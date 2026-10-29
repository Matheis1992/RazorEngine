using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Web.Razor;

namespace Test.RazorEngine
{
    /// <summary>
    /// Tests the IsolatedRazorEngineService.
    /// </summary>
    [TestFixture]
    public class IsolatedRazorEngineServiceTestFixture
    {
        /// <summary>
        /// Check if we are running on mono.
        /// </summary>
        /// <returns></returns>
        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        /// <summary>
        /// Check if we have either an SecurityManager enabled or are running on mono
        /// and ignore the test.
        /// </summary>
        public static void CheckMono()
        {
#pragma warning disable 0618 // Mono recommends to do this.
            if (!SecurityManager.SecurityEnabled)
                Assert.Ignore("SecurityManager.SecurityEnabled is OFF");
#pragma warning restore 0618
            if (IsRunningOnMono())
                Assert.Ignore("IsolatedRazorEngineServiceTestFixture is not supported on mono");
        }

        /// <summary>
        /// Creates a sandbox for testing.
        /// </summary>
        /// <returns></returns>
        public static AppDomain SandboxCreator()
        {
            return SandboxCreator(null);
        }

        /// <summary>
        /// Creates a sandbox for testing.
        /// </summary>
        /// <param name="permissions">Additional permissions</param>
        /// <returns></returns>
        public static AppDomain SandboxCreator(IEnumerable<IPermission> permissions)
        {
            CheckMono();

            Evidence ev = new Evidence();
            ev.AddHostEvidence(new Zone(SecurityZone.Internet));
            PermissionSet permSet = SecurityManager.GetStandardSandbox(ev);
            if (permissions != null)
            {
                foreach (IPermission permission in permissions)
                {
                    permSet.AddPermission(permission);
                }
            }
            // We have to load ourself with full trust
            StrongName razorEngineAssembly = typeof(RazorEngineService).Assembly.Evidence.GetHostEvidence<StrongName>();
            // We have to load Razor with full trust (so all methods are SecurityCritical)
            // This is because we apply AllowPartiallyTrustedCallers to RazorEngine, because
            // We need the untrusted (transparent) code to be able to inherit TemplateBase.
            // Because in the normal environment/appdomain we run as full trust and the Razor assembly has no security attributes
            // it will be completely SecurityCritical. 
            // This means we have to mark a lot of our members SecurityCritical (which is fine).
            // However in the sandbox domain we have partial trust and because razor has no Security attributes that means the
            // code will be transparent (this is where we get a lot of exceptions, because we now have different security attributes)
            // To work around this we give Razor full trust in the sandbox as well.
            StrongName razorAssembly = typeof(RazorTemplateEngine).Assembly.Evidence.GetHostEvidence<StrongName>();
            // We trust ourself as well
            StrongName testAssembly = typeof(IsolatedRazorEngineServiceTestFixture).Assembly.Evidence.GetHostEvidence<StrongName>();
            AppDomainSetup adSetup = new AppDomainSetup();
            adSetup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            AppDomain newDomain = AppDomain.CreateDomain("Sandbox", null, adSetup, permSet, razorEngineAssembly, razorAssembly, testAssembly);
            return newDomain;
        }

        /// <summary>
        /// Creates a fully trusted application domain, the only kind supported for isolation.
        /// </summary>
        /// <returns></returns>
        public static AppDomain FullTrustDomainCreator()
        {
            AppDomainSetup adSetup = new AppDomainSetup();
            adSetup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            // Use the same configuration file, so the binding redirects for Roslyn's dependencies apply in the new domain.
            adSetup.ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            return AppDomain.CreateDomain("Isolated", null, adSetup);
        }

        /// <summary>
        /// Tests that a partially trusted (sandboxed) application domain is refused (CVE-2021-46703)
        /// and unloaded again.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_WillThrowException_WhenUsingSandbox()
        {
            AppDomain sandbox = null;
            Assert.Throws<NotSupportedException>(() =>
            {
                using (var service = IsolatedRazorEngineService.Create(() => sandbox = SandboxCreator()))
                { }
            });

            Assert.That(sandbox, Is.Not.Null);
            Assert.Throws<AppDomainUnloadedException>(() => { Console.WriteLine(sandbox.FriendlyName); });
        }

        /// <summary>
        /// Tests that a simple viewbag is working.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_DynamicViewBag()
        {
            using (var service = IsolatedRazorEngineService.Create(FullTrustDomainCreator))
            {
                const string template = "<h1>Hello @ViewBag.Test</h1>";
                const string expected = "<h1>Hello TestItem</h1>";
                dynamic viewbag = new DynamicViewBag();
                viewbag.Test = "TestItem";

                string result = service.RunCompile(template, "test", (Type)null, (object)null, (DynamicViewBag)viewbag);
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Tests that exchanging values on the viewbag works across app domains
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_DynamicViewBag_FromIsolatedDomain()
        {
            using (var service = IsolatedRazorEngineService.Create(FullTrustDomainCreator))
            {
                const string template = "@{ ViewBag.Test = \"TestItem\"; }";
                const string expected = "TestItem";
                dynamic viewbag = new DynamicViewBag();

                string result = service.RunCompile(template, "test", (Type)null, (object)null, (DynamicViewBag)viewbag);
                Assert.That((string)viewbag.Test, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Tests that a simple viewbag is working.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_Dynamic()
        {
            using (var service = IsolatedRazorEngineService.Create(FullTrustDomainCreator))
            {
                const string template = "<h1>Hello @Model.Forename, @ViewBag.Test</h1>";
                const string expected = "<h1>Hello Matt, TestItem</h1>";
                dynamic viewbag = new DynamicViewBag();
                viewbag.Test = "TestItem";
                viewbag.Forename = "Matt";
                string result = service.RunCompile(template, "test", null, (object)viewbag, (DynamicViewBag)viewbag);
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Helper class to check the state of the remote AppDomain.
        /// </summary>
        public class AssemblyChecker : global::RazorEngine.CrossAppDomainObject
        {
            /// <summary>
            /// The assemblies are called "@(namespace).@(class).dll"
            /// </summary>
            const string CompiledAssemblyPrefix = CompilerServiceBase.DynamicTemplateNamespace + "." + CompilerServiceBase.ClassNamePrefix;

            /// <summary>
            /// Check if a compiled assembly exists.
            /// </summary>
            /// <returns></returns>
            public bool ExistsCompiledAssembly()
            {
                (new PermissionSet(PermissionState.Unrestricted)).Assert();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var razorEngine =
                    assemblies
                    .Where(a => a.FullName.StartsWith(CompiledAssemblyPrefix))
                    .FirstOrDefault();
                return razorEngine != null;
            }

            /// <summary>
            /// Check if everything is cleaned up.
            /// </summary>
            public void IsolatedRazorEngineService_CleanUpWorks()
            {
                var appDomain = FullTrustDomainCreator();
                using (var service = IsolatedRazorEngineService.Create(() => appDomain))
                {
                    string template = @"@Model.Name";

                    var result = service.RunCompile(template, "test", null, new { Name = "test" });
                    Assert.That(result, Is.EqualTo("test"));


                    ObjectHandle handle =
                        Activator.CreateInstanceFrom(
                            appDomain, typeof(AssemblyChecker).Assembly.ManifestModule.FullyQualifiedName,
                            typeof(AssemblyChecker).FullName
                        );

                    using (var localHelper = new AssemblyChecker())
                    {
                        Assert.False(localHelper.ExistsCompiledAssembly());
                    }
                    using (var remoteHelper = (AssemblyChecker)handle.Unwrap())
                    {
                        Assert.That(remoteHelper.ExistsCompiledAssembly());
                    }
                }
                Assert.Throws<AppDomainUnloadedException>(() => { Console.WriteLine(appDomain.FriendlyName); });
                using (var localHelper = new AssemblyChecker())
                {
                    Assert.False(localHelper.ExistsCompiledAssembly());
                }
            }
        }

        /// <summary>
        /// Tests that a bad template cannot do stuff
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CleanUpWorks()
        {
            var current = AppDomain.CurrentDomain;
            var appDomain = AppDomain.CreateDomain("TestDomain.IsolatedRazorEngineService_CleanUpWorks", current.Evidence, current.SetupInformation);

            ObjectHandle handle =
                Activator.CreateInstanceFrom(
                    appDomain, typeof(AssemblyChecker).Assembly.ManifestModule.FullyQualifiedName,
                    typeof(AssemblyChecker).FullName
                );
            using (var remoteHelper = (AssemblyChecker)handle.Unwrap())
            {
                remoteHelper.IsolatedRazorEngineService_CleanUpWorks();
            }
        }

        /// <summary>
        /// Tests that a simple template without a model can be parsed.
        /// </summary>
        [Test]
        public void RazorEngineService_VeryBadTemplate()
        {
            RazorEngineServiceTestFixture.RunTestHelper(service =>
            {
                string file = Path.Combine(Environment.CurrentDirectory, Path.GetRandomFileName());

                string template = @"
@using System.IO
@using System.Security
@using System.Security.Permissions
@{
(new PermissionSet(PermissionState.Unrestricted)).Assert();
File.WriteAllText(""$file$"", ""BAD DATA"");
}".Replace("$file$", file.Replace("\\", "\\\\"));
                var test = service.RunCompile(template, "test");

                Assert.That(new FileInfo(file), Does.Exist);
                File.Delete(file);
            });
        }

        /// <summary>
        /// Tests that a simple template without a model can be parsed.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CanParseSimpleTemplate_WithNoModel()
        {
            using (var service = IsolatedRazorEngineService.Create())
            {
                const string template = "<h1>Hello World</h1>";
                const string expected = template;

                string result = service.RunCompile(template, "test");

                Assert.That(result == expected, "Result does not match expected: " + result);
            }
        }

        /// <summary>
        /// Test that we can access static models in an isolated domain.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_StaticModel()
        {
            using (var service = IsolatedRazorEngineService.Create(FullTrustDomainCreator))
            {
                const string template = "<h1>Hello World @Model.Name</h1>";
                const string expected = "<h1>Hello World TestForename</h1>";
                // We "abuse" NameOnlyTemplateKey because it is SecurityTransparent and serializable.
                var model = new NameOnlyTemplateKey("TestForename", ResolveType.Global, null);
                string result = service.RunCompile(template, "test", typeof(NameOnlyTemplateKey), model);
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Test that we can access static models via "dynamic" in an isolated domain.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_StaticModelDynamicType()
        {
            using (var service = IsolatedRazorEngineService.Create(FullTrustDomainCreator))
            {
                const string template = "<h1>Hello @Model.Name</h1>";
                const string expected = "<h1>Hello Matt</h1>";
                // We "abuse" NameOnlyTemplateKey because it is SecurityTransparent and serializable.
                var model = new NameOnlyTemplateKey("Matt", ResolveType.Global, null);
                string result = service.RunCompile(template, "test", null, model);
                Assert.That(result, Is.EqualTo(expected));

            }
        }

        /// <summary>
        /// Tests that a simple template with a model can be parsed.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CanParseSimpleTemplate_WithComplexSerializableModel()
        {
            using (var service = IsolatedRazorEngineService.Create())
            {
                const string template = "<h1>Hello @Model.Forename</h1>";
                const string expected = "<h1>Hello Matt</h1>";

                var model = new Person { Forename = "Matt" };
                string result = service.RunCompile(template, "test", typeof(Person), model);

                Assert.That(result == expected, "Result does not match expected: " + result);
            }
        }

        /// <summary>
        /// Tests that a simple template with a non-serializable model cannot be parsed.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CannotParseSimpleTemplate_WithComplexNonSerializableModel()
        {
            using (var service = IsolatedRazorEngineService.Create())
            {
                const string template = "<h1>Animal Type: @Model.Type</h1>";

                Assert.Throws<SerializationException>(() =>
                {
                    var model = new Animal { Type = "Cat" };
                    service.RunCompile(template, "test", typeof(Animal), model);
                });
            }
        }

        /// <summary>
        /// Tests that a simple template with an anonymous model can be parsed.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_ParseSimpleTemplate_WithAnonymousModel()
        {
            using (var service = IsolatedRazorEngineService.Create())
            {
                const string template = "<h1>Animal Type: @Model.Type</h1>";
                const string expected = "<h1>Animal Type: Cat</h1>";

                var model = new { Type = "Cat" };
                var result = service.RunCompile(template, "test", null, new RazorDynamicObject(model));
                Assert.That(result, Is.EqualTo(expected));
            }
        }


        /// <summary>
        /// Tests that a simple template with an expando model cannot be parsed.
        /// </summary>
        /// <remarks>
        /// This may seem pointless to test, as the <see cref="IsolatedTemplateService"/> will explicitly
        /// check and throw the exception, it's worth creating a test for future reference. It's also
        /// something we can check should we ever find a way to support dynamic/anonymous objects
        /// across application domain boundaries.
        /// </remarks>
        [Test]
        public void IsolatedRazorEngineService_ParseSimpleTemplate_WithExpandoModel()
        {
            using (var service = IsolatedRazorEngineService.Create())
            {
                const string template = "<h1>Animal Type: @Model.Type</h1>";
                const string expected = "<h1>Animal Type: Cat</h1>";

                dynamic model = new ExpandoObject();
                model.Type = "Cat";
                var result = service.RunCompile(template, "test", null, (object)RazorDynamicObject.Create(model));
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Tests that a simple template with a dynamic model cannot be parsed.
        /// </summary>
        /// <remarks>
        /// This may seem pointless to test, as the <see cref="IsolatedTemplateService"/> will explicitly
        /// check and throw the exception, it's worth creating a test for future reference. It's also
        /// something we can check should we ever find a way to support dynamic/anonymous objects
        /// across application domain boundaries.
        /// </remarks>
        [Test]
        public void IsolatedRazorEngineService_CannotParseSimpleTemplate_WithDynamicModel()
        {
            using (var service = IsolatedRazorEngineService.Create())
            {
                const string template = "<h1>Animal Type: @Model.Type</h1>";
                const string expected = "<h1>Animal Type: Cat</h1>";

                dynamic model = new ValueObject(new Dictionary<string, object> { { "Type", "Cat" } });
                string result = service.RunCompile(template, "test", null, (object)RazorDynamicObject.Create(model));
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Tests that a simple template with an ISerializable model can be parsed in an isolated domain.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_WithISerializableModel()
        {
            using (var service = IsolatedRazorEngineService.Create(FullTrustDomainCreator))
            {
                const string template = "<h1>Uri Host: @Model.Host</h1>";
                const string expected = "<h1>Uri Host: example.com</h1>";

                var model = new Uri("http://example.com");
                var result = service.RunCompile(template, "test", null, (object)RazorDynamicObject.Create(model));
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        /// <summary>
        /// Tests that an isolated template service cannot use the same application domain as the 
        /// main application domain.
        /// </summary>
        /// <remarks>
        /// An isolated template service will unload it's child application domain on Dispose. We need to ensure
        /// it doesn't attempt to unload the current application domain that it is running in. This may or may
        /// not be the main application domain (but is very likely to be).
        /// </remarks>
        [Test]
        public void IsolatedRazorEngineService_WillThrowException_WhenUsingMainAppDomain()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var service = IsolatedRazorEngineService.Create(() => AppDomain.CurrentDomain))
                { }
            });
        }

        /// <summary>
        /// Tests that an isolated template service cannot use a null application domain.
        /// </summary>
        /// <remarks>
        /// I had considered using the default <see cref="IAppDomainFactory"/> to spawn a default
        /// application domain to load templates into when a null value is returned, but behaviourly this didn't 
        /// seem like the right thing to do. If you're using an <see cref="IsolatedTemplateService"/>, 
        /// you should expect it to have a valid application domain, so passing null should cause an exception.
        /// </remarks>
        [Test]
        public void IsolatedRazorEngineService_WillThrowException_WhenUsingNullAppDomain()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var service = IsolatedRazorEngineService.Create(() => null))
                { }
            });
        }

        /// <summary>
        /// Test Type.
        /// </summary>
        [Serializable]
        public class InsecureConfigCreator : IsolatedRazorEngineService.IConfigCreator
        {
            /// <summary>
            /// Test Type.
            /// </summary>
            public ITemplateServiceConfiguration CreateConfiguration()
            {
                var config = new TemplateServiceConfiguration();
                config.DisableTempFileLocking = true;
                return config;
            }
        }

        /// <summary>
        /// Tests that we cannot create an insecure sandbox.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_WillThrowException_WhenUsingDisableFileLocking()
        {
            Assert.Throws<InvalidOperationException>(() =>
                {
                    using (var service = IsolatedRazorEngineService.Create(new InsecureConfigCreator()))
                    {
                        const string template = "<h1>Hello World</h1>";
                        const string expected = template;

                        string result = service.RunCompile(template, "test");

                        Assert.That(result == expected, "Result does not match expected: " + result);
                    }
                });
        }

        /// <summary>
        /// Test Type.
        /// </summary>
        [Serializable]
        class ReferenceResolverConfigCreator : IsolatedRazorEngineService.IConfigCreator, IReferenceResolver
        {
            /// <summary>
            /// Test Type.
            /// </summary>
            public ITemplateServiceConfiguration CreateConfiguration()
            {
                var config = new TemplateServiceConfiguration();
                config.ReferenceResolver = this;
                config.CompilerServiceFactory = new DefaultCompilerServiceFactory();
                return config;
            }

            public IEnumerable<CompilerReference> GetReferences(TypeContext context, IEnumerable<CompilerReference> includeAssemblies = null)
            {
                // We need to return this standard set or even simple views blow up on
                // a missing reference to System.Linq.
                var loadedAssemblies = (new UseCurrentAssembliesReferenceResolver()).GetReferences(null);
                foreach (var reference in loadedAssemblies)
                    yield return reference;

                var testHelper = Path.Combine(
                    Path.GetDirectoryName(typeof(ReferenceResolverConfigCreator).Assembly.Location),
                    "test",
                    "TestHelper.dll");

                yield return CompilerReference.From(testHelper);
            }
        }

        /// <summary>
        /// Tests that we can use types from other assemblies in templates.
        /// Even when the type can be loaded.
        /// </summary>
        [Test]
        public void IsolatedRazorEngineService_CheckThatWeCanUseUnknownTypesAtExecuteTime()
        {
            using (var service = IsolatedRazorEngineService.Create(new ReferenceResolverConfigCreator(), FullTrustDomainCreator))
            {
                var template = @"
@{
    var t = new TestHelper.TestClass();
}
@t.TestProperty";
                const string expected = "\n\nTestPropert";

                string result = service.RunCompile(template, "test");

                Assert.That(result == expected, "Result does not match expected: " + result);
            }
        }

    }
}
