using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using RazorEngine.Compilation.ReferenceResolver;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Test.RazorEngine
{
    /// <summary>
    /// Tests for the default <see cref="UseCurrentAssembliesReferenceResolver"/>.
    /// </summary>
    [TestFixture]
    public class ReferenceResolverTestFixture
    {
        private static readonly Lazy<Assembly> OriginalRazorEngineStandIn = new Lazy<Assembly>(LoadOriginalRazorEngineStandIn);

        /// <summary>
        /// Emits and loads an assembly named like the original package ("RazorEngine") that defines a
        /// conflicting RazorEngine.Templating.TemplateBase&lt;T&gt;, as if the 3.x package were loaded next to this fork.
        /// </summary>
        private static Assembly LoadOriginalRazorEngineStandIn()
        {
            var compilation = CSharpCompilation.Create(
                "RazorEngine",
                new[] { CSharpSyntaxTree.ParseText("namespace RazorEngine.Templating { public class TemplateBase<T> { } }") },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var directory = Path.Combine(Path.GetTempPath(), "RazorEngineStandIn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "RazorEngine.dll");
            var result = compilation.Emit(file);
            Assert.That(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return Assembly.LoadFrom(file);
        }

        /// <summary>
        /// The assembly of the original RazorEngine package must not be passed to the template compiler.
        /// </summary>
        [Test]
        public void UseCurrentAssembliesReferenceResolver_SkipsOriginalRazorEngineAssembly()
        {
            var standIn = OriginalRazorEngineStandIn.Value;

            var references = new UseCurrentAssembliesReferenceResolver().GetReferences().ToList();

            Assert.That(references
                .OfType<CompilerReference.DirectAssemblyReference>()
                .Any(r => r.Assembly == standIn), Is.False);
            Assert.That(references
                .OfType<CompilerReference.DirectAssemblyReference>()
                .Any(r => r.Assembly == typeof(ITemplate).Assembly));
        }

        /// <summary>
        /// Templates still compile when the original RazorEngine assembly is loaded in the same process.
        /// </summary>
        [Test]
        public void RazorEngineService_CompilesTemplate_WhenOriginalRazorEngineIsLoaded()
        {
            OriginalRazorEngineStandIn.Value.GetTypes();

            using (var service = RazorEngineService.Create())
            {
                var result = service.RunCompile("Hello @Model.Forename", "standInTemplate", typeof(Person), new Person { Forename = "World" });
                Assert.That(result, Is.EqualTo("Hello World"));
            }
        }
    }
}
