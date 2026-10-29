using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Configuration;
using RazorEngine.Roslyn;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using RazorEngine.Text;
using System;
using System.IO;
using System.Linq;

namespace Test.RazorEngine.Core.Roslyn
{
    /// <summary>
    /// Some tests for the Roslyn Compiler backend.
    /// </summary>
    [TestFixture]
    public class RoslynCompilerTestFixture
    {
        /// <summary>
        /// Helper to run tests.
        /// </summary>
        /// <param name="test"></param>
        /// <param name="withConfig"></param>
        public static void RunTestHelper(Action<IRazorEngineService> test, Action<TemplateServiceConfiguration> withConfig = null)
        {
            var defaultConfig = new Action<TemplateServiceConfiguration>((config) => {
                    config.Debug = true;
                    config.CompilerServiceFactory = new RoslynCompilerServiceFactory();
                });
            if (withConfig == null)
            {
                withConfig = defaultConfig;
            }
            else
            {
                var oldFunc = withConfig;
                withConfig = (config) =>
                {
                    defaultConfig(config);
                    oldFunc(config);
                };
            }
            try
            {
                var config = new TemplateServiceConfiguration();
                withConfig(config);
                using (var service = RazorEngineService.Create(config))
                {
                    test(service);
                }
            }
            catch (TemplateCompilationException e)
            {
                var source = e.CompilationData.SourceCode;
                Console.WriteLine("Generated source file: \n\n{0}", source ?? "SOURCE CODE NOT AVAILABLE!");
                e.CompilationData.DeleteAll();
                throw;
            }
        }


        /// <summary>
        /// Tests that a IEnumerable with anonymous objects works.
        /// </summary>
        [Test]
        public void Roslyn_DynamicIEnumerable()
        {
            RunTestHelper(service =>
            {
                const string template = @"@Enumerable.Count(Model.Data)";
                const string expected = "3";
                var anonArray = new[] { new { InnerData = 1 }, new { InnerData = 2 }, new { InnerData = 3 } };
                var model = new { Data = anonArray.Select(a => a) };
                string result = service.RunCompile(template, "test", null, model, null);

                Assert.That(result == expected, "Result does not match expected: " + result);
            });
        }

        /// <summary>
        /// Test that anonymous types within the template work.
        /// </summary>
        [Test]
        public void Roslyn_AnonymousTypeWithinTemplate_2()
        {
            RunTestHelper(service =>
            {
                const string template_child = "@Enumerable.Count(Model.Data)";
                const string template_parent = @"@Include(""Child"", new { Data = Model.Animals})";
                const string expected = "3";
                service.Compile(template_child, "Child", null);
                var anonArray = new[] { new Animal { Type = "1" }, new Animal { Type = "2" }, new Animal { Type = "3" } };
                var model = new AnimalViewModel { Animals = anonArray };
                string result = service.RunCompile(template_parent, "test", typeof(AnimalViewModel), model, null);

                Assert.That(result == expected, "Result does not match expected: " + result);
            });
        }

        /// <summary>
        /// Test that anonymous types within the template work.
        /// </summary>
        [Test]
        public void Roslyn_AnonymousTypeWithinTemplate()
        {
            RunTestHelper(service =>
            {
                const string template_child = "@Enumerable.Count(Model.Data)";
                const string template_parent = @"@Include(""Child"", new { Data = Model.Data})";
                const string expected = "3";
                service.Compile(template_child, "Child", null);
                var anonArray = new[] { new { InnerData = 1 }, new { InnerData = 2 }, new { InnerData = 3 } };
                var model = new { Data = anonArray.Select(a => a) };
                string result = service.RunCompile(template_parent, "test", null, model, null);

                Assert.That(result == expected, "Result does not match expected: " + result);
            });
        }

        /// <summary>
        /// Tests that a specific encoding works.
        /// </summary>
        [Test]
        public void Roslyn_WithSpecificEncoding()
        {
            RunTestHelper(service =>
            {
                const string template = "<h1>Hello @Model.String</h1>";
                const string expected = "<h1>Hello Matt & World</h1>";

                var model = new { String = "Matt & World" };
                string result = service.RunCompile(template, "test", null, model, null);

                Assert.That(result == expected, "Result does not match expected: " + result);
            }, (c) => c.EncodedStringFactory = new RawStringFactory());
        }

        /// <summary>
        /// Tests that the compilation message is useful.
        /// </summary>
        [Test]
        public void Roslyn_GetInformativeErrorMessage()
        {
            RunTestHelper(service =>
            {
                const string template = "@foreach (var i in Model.Unknown) { @i }";
                var exn = Assert.Throws<TemplateCompilationException>(() =>
                {
                    string result = service.RunCompile(template, "test", typeof(object), new object());
                });
                exn.CompilationData.DeleteAll();
                var msg = exn.Message;

                // Compiler error
                Assert.That(msg, Does.Contain("does not contain a definition for"), "compiler error");
                // Template
                Assert.That(msg, Does.Contain(template), "template");
                // Temp files
                Assert.That(msg, Does.Contain("Temporary files of the compilation can be found"), "temp files");
                // C# source code
                Assert.That(msg, Does.Contain("namespace " + CompilerServiceBase.DynamicTemplateNamespace), "C# source");
            });
        }

        /// <summary>
        /// Tests that a runtime exception is useful.
        /// </summary>
        [Test]
        public void Roslyn_GetInformativeRuntimeErrorMessage()
        {
            RunTestHelper(service =>
            {
                const string template = "@foreach (var i in Model.Unknown) { @i }";
                string file = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(file, template);
                    var source = new LoadedTemplateSource(template, file);
                    var exn = Assert.Throws<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>(() =>
                    {
                        string result = service.RunCompile(source, "test", null, new object());
                    });
                    // We now have a reference to our template in the stacktrace
                    var stack = exn.StackTrace.ToLowerInvariant();
                    var expected = file.ToLowerInvariant();
                    Assert.That(
                        stack.Contains(expected),
                        "Could not find reference to template (" + expected + ") in stacktrace: \n" +
                        stack);
                }
                finally
                {
                    File.Delete(file);
                }
            });
        }

        /// <summary>
        /// Tests whether we can delete tempfiles when DisableTempFileLocking is true.
        /// </summary>
        [Test]
        public void Roslyn_TestDisableTempFileLocking()
        {
            var cache = new DefaultCachingProvider(t => { });
            var template = "@Model.Property";
            RunTestHelper(service =>
            {
                var model = new { Property = 0 };
                string result = service.RunCompile(template, "key", null, model);
                Assert.That(result.Trim(), Is.EqualTo("0"));
            }, config =>
            {
                config.Debug = false;
                config.CachingProvider = cache;
                config.DisableTempFileLocking = true;
            });
            ICompiledTemplate compiledTemplate;
            Assert.That(cache.TryRetrieveTemplate(new NameOnlyTemplateKey("key", ResolveType.Global, null), null, out compiledTemplate));
            var data = compiledTemplate.CompilationData;
            var folder = data.TmpFolder;
            Assert.That(new DirectoryInfo(folder), Does.Exist);
            data.DeleteAll();
            Assert.That(new DirectoryInfo(folder), Does.Not.Exist);
        }
    }
}
