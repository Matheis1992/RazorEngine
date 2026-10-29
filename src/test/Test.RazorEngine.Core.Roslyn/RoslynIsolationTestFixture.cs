using NUnit.Framework;
using RazorEngine.Configuration;
using RazorEngine.Roslyn;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using System;

namespace Test.RazorEngine.Core.Roslyn
{
    /// <summary>
    /// Tests Roslyn inside <see cref="IsolatedRazorEngineService"/>, set up as the documentation recommends:
    /// an <see cref="IsolatedRazorEngineService.IConfigCreator"/> that sets <see cref="RoslynCompilerServiceFactory"/>.
    /// </summary>
    [TestFixture]
    public class RoslynIsolationTestFixture
    {
        /// <summary>
        /// Creates the configuration with the Roslyn compiler in the isolated <see cref="AppDomain"/>.
        /// </summary>
        [Serializable]
        private sealed class RoslynConfigCreator : IsolatedRazorEngineService.IConfigCreator
        {
            public ITemplateServiceConfiguration CreateConfiguration()
            {
                return new TemplateServiceConfiguration { CompilerServiceFactory = new RoslynCompilerServiceFactory() };
            }
        }

        /// <summary>
        /// Creates a fully trusted application domain with the same configuration file (binding redirects for Roslyn's dependencies).
        /// </summary>
        private static AppDomain FullTrustDomainCreator()
        {
            var setup = new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
            };
            return AppDomain.CreateDomain("IsolatedRoslyn", null, setup);
        }

        /// <summary>
        /// A template with C# 6 syntax (string interpolation, which CodeDom's C# 5 compiler rejects) compiles with Roslyn
        /// in the isolated <see cref="AppDomain"/>, with a serializable model crossing the boundary.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CompilesWithRoslyn()
        {
            using (var service = IsolatedRazorEngineService.Create(new RoslynConfigCreator(), FullTrustDomainCreator))
            {
                var result = service.RunCompile("@($\"Hello {Model.Forename}!\")", "isolated-roslyn", typeof(Person), new Person { Forename = "Matt" });
                Assert.That(result, Is.EqualTo("Hello Matt!"));
            }
        }
    }
}
