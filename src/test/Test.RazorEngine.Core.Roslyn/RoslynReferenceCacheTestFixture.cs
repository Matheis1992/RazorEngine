using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;
using RazorEngine.Roslyn.CSharp;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Test.RazorEngine.Core.Roslyn
{
    /// <summary>
    /// The Roslyn compiler service caches the metadata references of loaded assemblies.
    /// These tests check that such a reference is reused, and that a reference given as a file path is read on every compile.
    /// </summary>
    [TestFixture]
    public class RoslynReferenceCacheTestFixture
    {
        /// <summary>
        /// A loaded assembly gets the same reference on every compile, so Roslyn can reuse what it read from it.
        /// </summary>
        [Test]
        public void Roslyn_ReusesReferenceOfLoadedAssembly()
        {
            var first = RoslynCompilerServiceBase.ToMetadataReference(CompilerReference.From(typeof(Uri).Assembly));
            var second = RoslynCompilerServiceBase.ToMetadataReference(CompilerReference.From(typeof(Uri).Assembly));
            Assert.That(second, Is.SameAs(first));
        }

        /// <summary>
        /// A reference given as a file path is read again on every compile, because the file can change.
        /// </summary>
        [Test]
        public void Roslyn_ReadsFileReferenceOnEveryCompile()
        {
            var file = typeof(Uri).Assembly.Location;
            var first = RoslynCompilerServiceBase.ToMetadataReference(CompilerReference.From(file));
            var second = RoslynCompilerServiceBase.ToMetadataReference(CompilerReference.From(file));
            Assert.That(second, Is.Not.SameAs(first));
        }

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
        /// A referenced file that is replaced between two compiles is read again, so the second template sees the new API.
        /// This also holds when the new file has the same size and modification time (for example after File.Copy,
        /// which keeps the modification time), because file references are not cached.
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
                    Assert.That(service.RunCompile("@(nameof(CacheTestLib.Greeter.Hello))", "first"), Is.EqualTo("Hello"));
                    Assert.That(service.RunCompile("@(nameof(CacheTestLib.Greeter.Hello)).", "second"), Is.EqualTo("Hello."));

                    // Replace the file: Hello is gone, Bye is new. Keep the old modification time and check the size
                    // is the same, so the file looks unchanged to anything that only compares those.
                    var before = new FileInfo(file);
                    var lastWrite = before.LastWriteTimeUtc;
                    var length = before.Length;
                    EmitLibrary(file, "Bye");
                    File.SetLastWriteTimeUtc(file, lastWrite);
                    Assert.That(new FileInfo(file).Length, Is.EqualTo(length), "The test needs both library versions to have the same size.");

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
