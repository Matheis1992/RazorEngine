using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using System;
using System.IO;
using System.Linq;

namespace Test.RazorEngine.Core.DotNetCompilerPlatform
{
    /// <summary>
    /// Tests the core package with the real Microsoft.CodeDom.Providers.DotNetCompilerPlatform, set up as doc/DotNetCompilerPlatform.md describes:
    /// <see cref="CodeDomCompilerServiceFactory.CodeDomProviderFactory"/> returns its <see cref="CSharpCodeProvider"/>,
    /// which compiles with the newer csc.exe that the package copies into roslyn\ of the output folder, in a separate process.
    /// </summary>
    [TestFixture]
    public class DotNetCompilerPlatformTestFixture
    {
        /// <summary>
        /// C# 7 (pattern matching) and C# 8 (switch expression): CodeDom's own C# 5 compiler rejects both.
        /// </summary>
        private const string ModernTemplate = "@{ var name = Model.Forename is string s ? s : \"nobody\"; }Hello @name, @(Model.Age switch { 0 => \"newborn\", _ => \"aged \" + Model.Age })!";

        private static readonly Person Matt = new Person { Forename = "Matt", Age = 40 };

        private static CodeDomCompilerServiceFactory CreateFactory(string compilerOptions = null)
        {
            return new CodeDomCompilerServiceFactory
            {
                CodeDomProviderFactory = () => new CSharpCodeProvider(),
                CompilerOptions = compilerOptions
            };
        }

        /// <summary>
        /// The package's build targets copied the compiler next to the test assembly, where the provider looks for it outside ASP.NET.
        /// </summary>
        [Test]
        public void Compiler_IsCopiedToRoslynFolder()
        {
            Assert.That(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "roslyn", "csc.exe"), Does.Exist);
        }

        /// <summary>
        /// A template with C# 7 and C# 8 syntax compiles with the provider's compiler.
        /// </summary>
        [Test]
        public void Provider_CompilesModernCSharp()
        {
            var config = new TemplateServiceConfiguration { CompilerServiceFactory = CreateFactory() };
            using (var service = RazorEngineService.Create(config))
            {
                Assert.That(service.RunCompile(ModernTemplate, "dncp-modern", typeof(Person), Matt), Is.EqualTo("Hello Matt, aged 40!"));
            }
        }

        /// <summary>
        /// Without the provider, the same template fails: the default CodeDom compiler is C# 5. This shows the provider makes the difference.
        /// </summary>
        [Test]
        public void Default_RejectsModernCSharp()
        {
            using (var service = RazorEngineService.Create())
            {
                var exception = Assert.Throws<TemplateCompilationException>(() => service.RunCompile(ModernTemplate, "dncp-default", typeof(Person), Matt));
                exception.CompilationData.DeleteAll();
            }
        }

        /// <summary>
        /// <see cref="CodeDomCompilerServiceFactory.CompilerOptions"/> reach the provider's compiler: with /langversion:7.3 the C# 8 switch expression is rejected.
        /// </summary>
        [Test]
        public void CompilerOptions_LimitTheLanguageVersion()
        {
            var config = new TemplateServiceConfiguration { CompilerServiceFactory = CreateFactory("/langversion:7.3") };
            using (var service = RazorEngineService.Create(config))
            {
                Assert.That(service.RunCompile("@(Model.Forename is string s ? s : \"nobody\")", "dncp-csharp73", typeof(Person), Matt), Is.EqualTo("Matt"));
                var exception = Assert.Throws<TemplateCompilationException>(() => service.RunCompile(ModernTemplate, "dncp-csharp73-switch", typeof(Person), Matt));
                Assert.That(exception.CompilerErrors.Select(e => e.ErrorText), Has.Some.Contains("8.0"));
                exception.CompilationData.DeleteAll();
            }
        }

        /// <summary>
        /// Creates the configuration with the provider in the isolated <see cref="AppDomain"/>: the lambda is created there,
        /// so it does not have to be serialized.
        /// </summary>
        [Serializable]
        private sealed class DotNetCompilerPlatformConfigCreator : IsolatedRazorEngineService.IConfigCreator
        {
            public ITemplateServiceConfiguration CreateConfiguration()
            {
                return new TemplateServiceConfiguration { CompilerServiceFactory = CreateFactory() };
            }
        }

        /// <summary>
        /// The provider works in <see cref="IsolatedRazorEngineService"/> with the default application domain factory.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CompilesWithProvider()
        {
            using (var service = IsolatedRazorEngineService.Create(new DotNetCompilerPlatformConfigCreator()))
            {
                Assert.That(service.RunCompile(ModernTemplate, "dncp-isolated", typeof(Person), Matt), Is.EqualTo("Hello Matt, aged 40!"));
            }
        }
    }
}
