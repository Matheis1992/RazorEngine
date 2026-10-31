using Microsoft.CSharp;
using NUnit.Framework;
using RazorEngine.Compilation.ReferenceResolver;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using System;
using System.CodeDom.Compiler;
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
            var directory = Path.Combine(Path.GetTempPath(), "RazorEngineStandIn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "RazorEngine.dll");

            // CodeDom, so the core tests do not need Roslyn.
            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters { GenerateExecutable = false, OutputAssembly = file };
                var result = provider.CompileAssemblyFromSource(parameters, "namespace RazorEngine.Templating { public class TemplateBase<T> { } }");
                Assert.That(result.Errors.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors.Cast<CompilerError>()));
            }
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
        /// The folder for the assemblies of <see cref="LoadEmptyAssembly"/>. Loaded assemblies cannot be deleted while the test process
        /// runs, so each run deletes what earlier runs left behind.
        /// </summary>
        private static readonly string EmptyAssembliesDirectory = Path.Combine(Path.GetTempPath(), "RazorEngineResolverTests");

        [OneTimeSetUp]
        public void DeleteEmptyAssembliesOfEarlierRuns()
        {
            if (!Directory.Exists(EmptyAssembliesDirectory))
            {
                return;
            }
            foreach (var directory in Directory.GetDirectories(EmptyAssembliesDirectory))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (IOException)
                {
                    // Still loaded by another test run.
                }
                catch (UnauthorizedAccessException)
                {
                    // Still loaded by another test run.
                }
            }
        }

        /// <summary>
        /// Compiles an empty assembly with the given name and version into its own folder and loads it with <see cref="Assembly.LoadFile"/>,
        /// so two versions with the same name can be loaded side by side.
        /// </summary>
        private static Assembly LoadEmptyAssembly(string name, string version)
        {
            var directory = Path.Combine(EmptyAssembliesDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, name + ".dll");

            using (var provider = new CSharpCodeProvider())
            {
                var parameters = new CompilerParameters { GenerateExecutable = false, OutputAssembly = file };
                var result = provider.CompileAssemblyFromSource(parameters, "[assembly: System.Reflection.AssemblyVersion(\"" + version + "\")] public class Empty { }");
                Assert.That(result.Errors.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors.Cast<CompilerError>()));
            }
            return Assembly.LoadFile(file);
        }

        private static Assembly[] ReferencedAssemblies()
        {
            return new UseCurrentAssembliesReferenceResolver().GetReferences()
                .OfType<CompilerReference.DirectAssemblyReference>()
                .Select(r => r.Assembly)
                .ToArray();
        }

        /// <summary>
        /// When two versions of an assembly are loaded, only the newest one is passed to the compiler.
        /// </summary>
        [Test]
        public void UseCurrentAssembliesReferenceResolver_PrefersNewestVersion()
        {
            var name = "Versioned_" + Guid.NewGuid().ToString("N");
            var older = LoadEmptyAssembly(name, "1.0.0.0");
            var newer = LoadEmptyAssembly(name, "2.0.0.0");

            var references = ReferencedAssemblies().Where(a => a.GetName().Name == name).ToArray();

            Assert.That(references, Is.EqualTo(new[] { newer }));
            Assert.That(references, Does.Not.Contain(older));
        }

        /// <summary>
        /// The resolver remembers its checks per assembly, but assemblies loaded after an earlier compile are still passed to the compiler.
        /// </summary>
        [Test]
        public void UseCurrentAssembliesReferenceResolver_IncludesAssembliesLoadedLater()
        {
            ReferencedAssemblies();

            var later = LoadEmptyAssembly("Later_" + Guid.NewGuid().ToString("N"), "1.0.0.0");

            Assert.That(ReferencedAssemblies(), Does.Contain(later));
        }

        /// <summary>
        /// Assemblies without a file (loaded from bytes) are not passed to the compiler.
        /// </summary>
        [Test]
        public void UseCurrentAssembliesReferenceResolver_SkipsAssembliesWithoutFile()
        {
            var fromFile = LoadEmptyAssembly("FromBytes_" + Guid.NewGuid().ToString("N"), "1.0.0.0");
            var fromBytes = Assembly.Load(File.ReadAllBytes(fromFile.Location));

            Assert.That(ReferencedAssemblies(), Does.Not.Contain(fromBytes));
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
