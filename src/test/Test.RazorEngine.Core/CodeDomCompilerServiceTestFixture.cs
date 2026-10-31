using Autofac;
using Autofac.Features.ResolveAnything;
using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using RazorEngine.Tests.TestTypes.Activation;
using System;
using System.Linq;

namespace RazorEngine.Tests
{
    /// <summary>
    /// Tests the CodeDom compiler service: <see cref="CodeDomCompilerServiceFactory"/>, <see cref="DefaultCompilerServiceFactory"/>
    /// (the default, also CodeDom) and their options. The core package compiles templates without Roslyn.
    /// </summary>
    [TestFixture]
    public class CodeDomCompilerServiceTestFixture
    {
        private static IRazorEngineService CreateService(TemplateServiceConfiguration config = null)
        {
            config = config ?? new TemplateServiceConfiguration();
            config.CompilerServiceFactory = new CodeDomCompilerServiceFactory();
            return RazorEngineService.Create(config);
        }

        /// <summary>
        /// A simple template with a static model compiles with CodeDom.
        /// </summary>
        [Test]
        public void CodeDom_CompilesTemplateWithModel()
        {
            using (var service = CreateService())
            {
                var result = service.RunCompile("Hello @Model.Forename!", "codedom-model", typeof(Person), new Person { Forename = "Matt" });
                Assert.That(result, Is.EqualTo("Hello Matt!"));
            }
        }

        /// <summary>
        /// Constructors of the base template type are generated for CodeDom too.
        /// </summary>
        [Test]
        public void CodeDom_SupportsBaseTemplateWithConstructorParameters()
        {
            var container = new ContainerBuilder();
            container.RegisterType<ReverseTextFormatter>().AsSelf().As<ITextFormatter>();
            container.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());

            var config = new TemplateServiceConfiguration
            {
                Activator = new AutofacTemplateActivator(container.Build()),
                BaseTemplateType = typeof(CustomTemplateBase<>)
            };

            using (var service = CreateService(config))
            {
                var result = service.RunCompile("<h1>Hello @Format(Model.Forename)</h1>", "codedom-ctor", typeof(Person), new Person { Forename = "Matt" });
                Assert.That(result, Is.EqualTo("<h1>Hello ttaM</h1>"));
            }
        }

        /// <summary>
        /// CodeDom only supports C# 5, so C# 6 features fail to compile (and prove that CodeDom is used).
        /// </summary>
        [Test]
        public void CodeDom_DoesNotSupportCSharp6()
        {
            using (var service = CreateService())
            {
                var exception = Assert.Throws<TemplateCompilationException>(() =>
                    service.RunCompile("@($\"Hello {Model.Forename}\")", "codedom-csharp6", typeof(Person), new Person { Forename = "Matt" }));
                exception.CompilationData.DeleteAll();
            }
        }

        /// <summary>
        /// The core assembly does not reference Roslyn or its dependencies; Roslyn is in RazorEngine.NetFramework.Roslyn.
        /// </summary>
        [Test]
        public void Core_DoesNotReferenceRoslyn()
        {
            var references = typeof(Engine).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

            Assert.That(references, Has.None.StartsWith("Microsoft.CodeAnalysis"));
            Assert.That(references, Has.None.EqualTo("System.Collections.Immutable"));
            Assert.That(references, Has.None.EqualTo("System.Reflection.Metadata"));
            Assert.That(references, Has.None.EqualTo("System.Memory"));
        }

        /// <summary>
        /// The default configuration compiles with CodeDom: a simple template works, C# 6 fails.
        /// </summary>
        [Test]
        public void Default_CompilesWithCodeDom()
        {
            Assert.That(new TemplateServiceConfiguration().CompilerServiceFactory, Is.InstanceOf<CodeDomCompilerServiceFactory>());
            using (var service = RazorEngineService.Create())
            {
                Assert.That(service.RunCompile("Hello @Model.Forename!", "default-codedom", typeof(Person), new Person { Forename = "Matt" }), Is.EqualTo("Hello Matt!"));
                var exception = Assert.Throws<TemplateCompilationException>(() =>
                    service.RunCompile("@($\"Hello {Model.Forename}\")", "default-csharp6", typeof(Person), new Person { Forename = "Matt" }));
                exception.CompilationData.DeleteAll();
            }
        }

        /// <summary>
        /// <see cref="CodeDomCompilerServiceFactory.CompilerOptions"/> reach the compiler: a symbol defined there is visible in the template.
        /// </summary>
        [Test]
        public void CodeDom_CompilerOptionsReachTheCompiler()
        {
            const string template = "@{\n#if MYSYMBOL\n    var symbol = \"defined\";\n#else\n    var symbol = \"missing\";\n#endif\n}@symbol";

            var config = new TemplateServiceConfiguration { CompilerServiceFactory = new CodeDomCompilerServiceFactory { CompilerOptions = "/define:MYSYMBOL" } };
            using (var service = RazorEngineService.Create(config))
            {
                Assert.That(service.RunCompile(template, "codedom-options"), Is.EqualTo("defined"));
            }
            using (var service = RazorEngineService.Create())
            {
                Assert.That(service.RunCompile(template, "codedom-no-options"), Is.EqualTo("missing"));
            }
        }

        /// <summary>
        /// A C# provider that counts its compiles and its disposal.
        /// </summary>
        private sealed class CountingCSharpCodeProvider : Microsoft.CSharp.CSharpCodeProvider
        {
            public static int Compiles;
            public static int Disposals;

            public override System.CodeDom.Compiler.CompilerResults CompileAssemblyFromSource(System.CodeDom.Compiler.CompilerParameters options, params string[] sources)
            {
                Compiles++;
                return base.CompileAssemblyFromSource(options, sources);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Disposals++;
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// <see cref="CodeDomCompilerServiceFactory.CodeDomProviderFactory"/> is used for compiling, and the compiler service disposes the provider.
        /// This is how a provider such as Microsoft.CodeDom.Providers.DotNetCompilerPlatform (modern C#) is plugged in.
        /// </summary>
        [Test]
        public void CodeDom_UsesAndDisposesProviderFromFactory()
        {
            CountingCSharpCodeProvider.Compiles = 0;
            CountingCSharpCodeProvider.Disposals = 0;
            var config = new TemplateServiceConfiguration
            {
                CompilerServiceFactory = new CodeDomCompilerServiceFactory { CodeDomProviderFactory = () => new CountingCSharpCodeProvider() }
            };
            using (var service = RazorEngineService.Create(config))
            {
                Assert.That(service.RunCompile("Hello @Model.Forename!", "codedom-provider", typeof(Person), new Person { Forename = "Matt" }), Is.EqualTo("Hello Matt!"));
            }

            Assert.That(CountingCSharpCodeProvider.Compiles, Is.EqualTo(1));
            Assert.That(CountingCSharpCodeProvider.Disposals, Is.EqualTo(1));
        }

        /// <summary>
        /// A <see cref="CodeDomCompilerServiceFactory.CodeDomProviderFactory"/> that returns null is reported
        /// instead of silently falling back to the C# 5 compiler.
        /// </summary>
        [Test]
        public void CodeDom_ProviderFactoryReturningNullThrows()
        {
            var factory = new CodeDomCompilerServiceFactory { CodeDomProviderFactory = () => null };
            Assert.Throws<InvalidOperationException>(() => factory.CreateCompilerService(Language.CSharp));
        }

        /// <summary>
        /// A compiler service that adjusts the compiler parameters and records the output assembly it saw.
        /// </summary>
        private sealed class HookCompilerService : Compilation.CSharp.CSharpDirectCompilerService
        {
            public static string SeenOutputAssembly;

            public HookCompilerService(System.CodeDom.Compiler.CodeDomProvider provider) : base(provider) { }

            protected override void ConfigureCompilerParameters(System.CodeDom.Compiler.CompilerParameters parameters)
            {
                SeenOutputAssembly = parameters.OutputAssembly;
                parameters.CompilerOptions += " /define:FROMHOOK";
            }
        }

        /// <summary>
        /// A factory that creates <see cref="HookCompilerService"/>.
        /// </summary>
        private sealed class HookCompilerServiceFactory : CodeDomCompilerServiceFactory
        {
            protected override DirectCompilerServiceBase CreateCSharpCompilerService(System.CodeDom.Compiler.CodeDomProvider codeDomProvider)
            {
                return new HookCompilerService(codeDomProvider ?? new Microsoft.CSharp.CSharpCodeProvider());
            }
        }

        /// <summary>
        /// A factory derived from <see cref="CodeDomCompilerServiceFactory"/> can create its own service, whose
        /// <see cref="DirectCompilerServiceBase.ConfigureCompilerParameters"/> runs after RazorEngine has set up the parameters
        /// (including the output assembly), and <see cref="CodeDomCompilerServiceFactory.CompilerOptions"/> still applies.
        /// </summary>
        [Test]
        public void CodeDom_FactoryCreatesOwnServiceWithParameterHook()
        {
            const string template = "@{\n#if FROMHOOK && MYSYMBOL\n    var symbol = \"both\";\n#else\n    var symbol = \"missing\";\n#endif\n}@symbol";

            HookCompilerService.SeenOutputAssembly = null;
            var config = new TemplateServiceConfiguration { CompilerServiceFactory = new HookCompilerServiceFactory { CompilerOptions = "/define:MYSYMBOL" } };
            using (var service = RazorEngineService.Create(config))
            {
                Assert.That(service.RunCompile(template, "codedom-hook"), Is.EqualTo("both"));
            }

            Assert.That(HookCompilerService.SeenOutputAssembly, Does.EndWith(".dll"));
        }

        /// <summary>
        /// A factory whose service creation fails.
        /// </summary>
        private sealed class FailingCompilerServiceFactory : CodeDomCompilerServiceFactory
        {
            protected override DirectCompilerServiceBase CreateCSharpCompilerService(System.CodeDom.Compiler.CodeDomProvider codeDomProvider)
            {
                throw new NotSupportedException("no service");
            }
        }

        /// <summary>
        /// The provider from <see cref="CodeDomCompilerServiceFactory.CodeDomProviderFactory"/> is disposed when no service owns it.
        /// </summary>
        [Test]
        public void CodeDom_ProviderIsDisposedWhenServiceCreationFails()
        {
            CountingCSharpCodeProvider.Disposals = 0;
            var factory = new FailingCompilerServiceFactory { CodeDomProviderFactory = () => new CountingCSharpCodeProvider() };

            Assert.Throws<NotSupportedException>(() => factory.CreateCompilerService(Language.CSharp));
            Assert.That(CountingCSharpCodeProvider.Disposals, Is.EqualTo(1));
        }
    }
}
