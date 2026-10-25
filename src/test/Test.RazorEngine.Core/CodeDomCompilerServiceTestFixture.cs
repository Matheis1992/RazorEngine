namespace RazorEngine.Tests
{
    using Autofac;
    using Autofac.Features.ResolveAnything;
    using NUnit.Framework;

    using Compilation;
    using Configuration;
    using Templating;
    using TestTypes;
    using TestTypes.Activation;

    /// <summary>
    /// Tests the <see cref="CodeDomCompilerServiceFactory"/> (CodeDom, C# 5), the opt-out from the Roslyn default.
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
    }
}
