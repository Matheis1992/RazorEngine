using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Test.RazorEngine.Core.Roslyn
{
    /// <summary>
    /// The Roslyn compiler service caches the metadata references of referenced files.
    /// These tests check that a cached reference is reused and that a changed file is read again.
    /// </summary>
    [TestFixture]
    public class RoslynReferenceCacheTestFixture
    {
        /// <summary>
        /// Adds one assembly file to the default references.
        /// </summary>
        private class FileReferenceResolver : IReferenceResolver
        {
            private readonly string _file;

            public FileReferenceResolver(string file)
            {
                _file = file;
            }

            public IEnumerable<CompilerReference> GetReferences(TypeContext context = null, IEnumerable<CompilerReference> includeAssemblies = null)
            {
                return new UseCurrentAssembliesReferenceResolver()
                    .GetReferences(context, includeAssemblies)
                    .Concat(new[] { CompilerReference.From(_file) });
            }
        }

        /// <summary>
        /// Writes an assembly CacheTestLib.dll with a class CacheTestLib.Greeter that has one static method.
        /// </summary>
        private static void EmitLibrary(string file, string methodName)
        {
            var compilation = CSharpCompilation.Create(
                "CacheTestLib",
                new[] { CSharpSyntaxTree.ParseText("namespace CacheTestLib { public static class Greeter { public static string " + methodName + "() { return \"\"; } } }") },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var result = compilation.Emit(file);
            Assert.That(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        }

        /// <summary>
        /// A referenced file that changes between two compiles is read again,
        /// so the second template sees the new API and not the cached old one.
        /// </summary>
        [Test]
        public void Roslyn_ReadsChangedReferencedFileAgain()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RazorEngineReferenceCache_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var file = Path.Combine(directory, "CacheTestLib.dll");
                EmitLibrary(file, "Hello");

                RoslynCompilerTestFixture.RunTestHelper(service =>
                {
                    // Compile twice with the same file: the second compile uses the cached reference.
                    Assert.That(service.RunCompile("@(nameof(CacheTestLib.Greeter.Hello))", "first"), Is.EqualTo("Hello"));
                    Assert.That(service.RunCompile("@(nameof(CacheTestLib.Greeter.Hello)).", "second"), Is.EqualTo("Hello."));

                    // Replace the file: Hello is gone, Bye is new. Move the timestamp on, so the change is visible
                    // even when both writes fall into the same file system time tick.
                    var before = File.GetLastWriteTimeUtc(file);
                    EmitLibrary(file, "Bye");
                    File.SetLastWriteTimeUtc(file, before.AddMinutes(1));

                    Assert.That(service.RunCompile("@(nameof(CacheTestLib.Greeter.Bye))", "third"), Is.EqualTo("Bye"));
                    Assert.Throws<TemplateCompilationException>(() =>
                        service.RunCompile("@(nameof(CacheTestLib.Greeter.Hello))", "fourth"));
                }, config =>
                {
                    config.ReferenceResolver = new FileReferenceResolver(file);
                    config.Debug = false;
                });
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
