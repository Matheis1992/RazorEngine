using Moq;
using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Text;
using System;
using System.IO;

namespace RazorEngine.Tests
{
    /// <summary>
    /// Defines a test fixture that provides tests for the <see cref="ITemplateServiceConfiguration"/> type.
    /// </summary>
    [TestFixture]
    public class ConfigurationTestFixture
    {
        #region Tests
        /// <summary>
        /// Tests that the fluent configuration supports adding additional namespace imports.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanAddNamespaces()
        {
            var config = new FluentTemplateServiceConfiguration(c => c.IncludeNamespaces("RazorEngine.Templating"));

            Assert.That(config.Namespaces, Does.Contain("RazorEngine.Templating"));
        }

        /// <summary>
        /// Tests that the fluent configuration can configure a template service with additional namespaces.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanConfigureTemplateService_WithAdditionalNamespaces()
        {
            var config = new FluentTemplateServiceConfiguration(
                c => c.IncludeNamespaces("System.IO"));
            using (var service = RazorEngineService.Create(config))
            using (var writer = new StringWriter())
            {
                const string template = @"@Directory.GetFiles(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Personal)), ""*.*"").Length";

                int expected = Directory.GetFiles(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Personal)), "*.*").Length;

                var key = service.GetKey("testTemplate");
                var source = new LoadedTemplateSource(template);
                service.AddTemplate(key, source);
                service.Compile(key);
                service.Run(key, writer);
                var result = writer.ToString();
                Assert.That(result, Is.EqualTo(expected.ToString()));
            }
        }

        /// <summary>
        /// Tests that the fluent configuration can configure a template service with a specific encoding.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanConfigureTemplateService_WithSpecificEncoding()
        {
            var config = new FluentTemplateServiceConfiguration(
                c => c.WithEncoding(Encoding.Raw));

            using (var service = RazorEngineService.Create(config))
            using (var writer = new StringWriter())
            {
                const string template = "<h1>Hello @Model.String</h1>";
                const string expected = "<h1>Hello Matt & World</h1>";

                var model = new { String = "Matt & World" };
                var source = new LoadedTemplateSource(template);
                var key = service.GetKey("testTemplate");

                service.Compile(source, key);
                service.Run(key, writer, model: model);

                var contents = writer.ToString();

                Assert.That(contents, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Tests that the fluent configuration supports setting a custom activator.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanSetActivator_UsingActivator()
        {
            var mock = new Mock<IActivator>();

            var config = new FluentTemplateServiceConfiguration(c => c.ActivateUsing(mock.Object));

            Assert.That(config.Activator == mock.Object);
        }

        /// <summary>
        /// Tests that the fluent configuration supports setting the compiler service factory.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanSetCompilerServiceFactory()
        {
            var mock = new Mock<ICompilerServiceFactory>();

            var config = new FluentTemplateServiceConfiguration(c => c.CompileUsing(mock.Object));

            Assert.That(config.CompilerServiceFactory == mock.Object);
        }

        /// <summary>
        /// Tests that the fluent configuration supports setting the encoded string factory.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanSetEncodedStringFactory()
        {
            var mock = new Mock<IEncodedStringFactory>();

            var config = new FluentTemplateServiceConfiguration(c => c.EncodeUsing(mock.Object));

            Assert.That(config.EncodedStringFactory == mock.Object);
        }

        /// <summary>
        /// Tests that the fluent configuration supports setting the encoded string factory using a predefined encoding.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanSetEncoding_UsingHtmlEncoding()
        {
            var config = new FluentTemplateServiceConfiguration(c => c.WithEncoding(Encoding.Html));

            Assert.That(config.EncodedStringFactory is HtmlEncodedStringFactory);
        }

        /// <summary>
        /// Tests that the fluent configuration supports setting the encoded string factory using a predefined encoding.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfiguration_CanSetEncoding_UsingRawEncoding()
        {
            var config = new FluentTemplateServiceConfiguration(c => c.WithEncoding(Encoding.Raw));

            Assert.That(config.EncodedStringFactory is RawStringFactory);
        }

        /// <summary>
        /// Tests that the fluent configuration supports setting a custom activator delegate.
        /// </summary>
        [Test]
        public void FluentTemplateServiceConfigutation_CanSetActivator_UsingDelegate()
        {
            Func<InstanceContext, ITemplate> activator = i => null;

            var config = new FluentTemplateServiceConfiguration(c => c.ActivateUsing(activator));
            var delegateActivator = (DelegateActivator)config.Activator;

            Assert.That(delegateActivator.Activator == activator);
        }

        /// <summary>
        /// Missing arguments are rejected right away. The checks used to be Code Contracts calls,
        /// which the compiler removes unless CONTRACTS_FULL is defined, so they never ran.
        /// </summary>
        [Test]
        public void NullArguments_ThrowArgumentNullException()
        {
            Assert.That(() => new FluentTemplateServiceConfiguration(null),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("config"));
            Assert.That(() => new FluentTemplateServiceConfiguration(c => c.ActivateUsing((Func<InstanceContext, ITemplate>)null)),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("activator"));
            Assert.That(() => new TypeLoader(null, new System.Reflection.Assembly[0]),
                Throws.TypeOf<ArgumentNullException>().With.Property("ParamName").EqualTo("appDomain"));
        }
        #endregion
    }
}