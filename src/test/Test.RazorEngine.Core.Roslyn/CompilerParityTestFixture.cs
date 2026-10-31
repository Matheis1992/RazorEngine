using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Configuration;
using RazorEngine.Roslyn;
using RazorEngine.Templating;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Test.RazorEngine.Core.Roslyn
{
    /// <summary>
    /// Checks that the Roslyn and the CodeDom (default) compiler services compile templates the same way.
    /// </summary>
    [TestFixture]
    public class CompilerParityTestFixture
    {
        /// <summary>
        /// The compiler service factories to compare.
        /// </summary>
        public static IEnumerable<TestCaseData> Factories()
        {
            yield return new TestCaseData(new RoslynCompilerServiceFactory()).SetArgDisplayNames("Roslyn");
            yield return new TestCaseData(new DefaultCompilerServiceFactory()).SetArgDisplayNames("CodeDom");
        }

        private static ICompiledTemplate Compile(ICompilerServiceFactory factory, string template, bool debug, out string result)
        {
            var cache = new DefaultCachingProvider();
            var config = new TemplateServiceConfiguration
            {
                CompilerServiceFactory = factory,
                CachingProvider = cache,
                Debug = debug
            };
            using (var service = RazorEngineService.Create(config))
            {
                result = service.RunCompile(template, "parity", null, (object)null);
            }

            ICompiledTemplate compiled;
            Assert.That(cache.TryRetrieveTemplate(new NameOnlyTemplateKey("parity", ResolveType.Global, null), null, out compiled));
            return compiled;
        }

        /// <summary>
        /// Templates can test for the RAZORENGINE preprocessor symbol.
        /// </summary>
        [TestCaseSource(nameof(Factories))]
        public void Compiler_DefinesRazorEngineSymbol(ICompilerServiceFactory factory)
        {
            const string template = "@{\n#if RAZORENGINE\n    var symbol = \"defined\";\n#else\n    var symbol = \"missing\";\n#endif\n}@symbol";

            string result;
            Compile(factory, template, false, out result);

            Assert.That(result, Is.EqualTo("defined"));
        }

        /// <summary>
        /// The compiler service factories combined with the Debug setting.
        /// </summary>
        public static IEnumerable<TestCaseData> FactoriesWithDebug()
        {
            foreach (var debug in new[] { false, true })
            {
                yield return new TestCaseData(new RoslynCompilerServiceFactory(), debug).SetArgDisplayNames("Roslyn", "Debug=" + debug);
                yield return new TestCaseData(new DefaultCompilerServiceFactory(), debug).SetArgDisplayNames("CodeDom", "Debug=" + debug);
            }
        }

        /// <summary>
        /// Templates are compiled with optimizations, also when Debug is set.
        /// </summary>
        [TestCaseSource(nameof(FactoriesWithDebug))]
        public void Compiler_OptimizesTemplates(ICompilerServiceFactory factory, bool debug)
        {
            string result;
            var compiled = Compile(factory, "Hello", debug, out result);

            var debuggable = compiled.TemplateType.Assembly.GetCustomAttribute<DebuggableAttribute>();
            Assert.That(debuggable == null || !debuggable.IsJITOptimizerDisabled, "The JIT optimizer is disabled for the template assembly.");
        }
    }
}
