using NUnit.Framework;
using RazorEngine.Compilation;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Tests.TestTypes;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace RazorEngine.Tests
{
    /// <summary>
    /// Tests that templates are compiled only once when several threads need them at the same time,
    /// and that the default caching provider does not leak <see cref="AppDomain.AssemblyResolve"/> handlers.
    /// </summary>
    [TestFixture]
    public class CompileOnceTestFixture
    {
        private const int Threads = 8;

        /// <summary>
        /// A CodeDom compiler service factory that counts the compiles (one compiler service per compile).
        /// </summary>
        private sealed class CountingCompilerServiceFactory : CodeDomCompilerServiceFactory
        {
            public int Compiles;

            protected override DirectCompilerServiceBase CreateCSharpCompilerService(System.CodeDom.Compiler.CodeDomProvider codeDomProvider)
            {
                Interlocked.Increment(ref Compiles);
                return base.CreateCSharpCompilerService(codeDomProvider);
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on <see cref="Threads"/> threads that start at the same time and returns the results in thread order.
        /// </summary>
        private static string[] RunInParallel(Func<int, string> action)
        {
            var results = new string[Threads];
            var errors = new Exception[Threads];
            using (var start = new Barrier(Threads))
            {
                var threads = Enumerable.Range(0, Threads).Select(i => new Thread(() =>
                {
                    try
                    {
                        start.SignalAndWait();
                        results[i] = action(i);
                    }
                    catch (Exception e)
                    {
                        errors[i] = e;
                    }
                })).ToList();
                threads.ForEach(t => t.Start());
                threads.ForEach(t => t.Join());
            }
            var error = errors.FirstOrDefault(e => e != null);
            if (error != null)
            {
                throw new AggregateException(error);
            }
            return results;
        }

        /// <summary>
        /// Threads that run the same uncached template at the same time compile it once; the others wait for that compile.
        /// </summary>
        [Test]
        public void RunCompile_CompilesOnceForConcurrentCacheMisses()
        {
            var factory = new CountingCompilerServiceFactory();
            using (var service = RazorEngineService.Create(new TemplateServiceConfiguration { CompilerServiceFactory = factory }))
            {
                service.AddTemplate("parallel", "Hello @Model.Forename!");

                var results = RunInParallel(i => service.RunCompile("parallel", typeof(Person), new Person { Forename = "T" + i }));

                Assert.That(factory.Compiles, Is.EqualTo(1));
                Assert.That(results, Is.EqualTo(Enumerable.Range(0, Threads).Select(i => "Hello T" + i + "!")));
            }
        }

        /// <summary>
        /// A failing compile is not repeated for every waiting thread: all threads that wait for it get its exception.
        /// The next call after that compiles again, so a fixed template source is picked up.
        /// </summary>
        [Test]
        public void RunCompile_SharesAFailedCompileAndRetriesLater()
        {
            var factory = new CountingCompilerServiceFactory();
            using (var service = RazorEngineService.Create(new TemplateServiceConfiguration { CompilerServiceFactory = factory }))
            {
                service.AddTemplate("broken", "@{ var x = ; }");

                var exception = Assert.Throws<AggregateException>(() => RunInParallel(i => service.RunCompile("broken", typeof(Person), new Person())));
                Assert.That(exception.InnerException, Is.InstanceOf<TemplateCompilationException>());
                ((TemplateCompilationException)exception.InnerException).CompilationData.DeleteAll();
                Assert.That(factory.Compiles, Is.EqualTo(1));

                var again = Assert.Throws<TemplateCompilationException>(() => service.RunCompile("broken", typeof(Person), new Person()));
                again.CompilationData.DeleteAll();
                Assert.That(factory.Compiles, Is.EqualTo(2));
            }
        }

        /// <summary>
        /// An included template that is not cached yet is compiled once when several threads run the including template at the same time.
        /// </summary>
        [Test]
        public void Include_CompilesOnceForConcurrentCacheMisses()
        {
            var factory = new CountingCompilerServiceFactory();
            using (var service = RazorEngineService.Create(new TemplateServiceConfiguration { CompilerServiceFactory = factory }))
            {
                service.AddTemplate("main", "Main @Include(\"part\")");
                service.AddTemplate("part", "Part");
                service.Compile("main", typeof(Person));
                Assert.That(factory.Compiles, Is.EqualTo(1));

                var results = RunInParallel(i => service.Run("main", typeof(Person), new Person()));

                Assert.That(factory.Compiles, Is.EqualTo(2));
                Assert.That(results, Has.All.EqualTo("Main Part"));
            }
        }

        /// <summary>
        /// The same template with another model type is another template: it is compiled separately.
        /// </summary>
        [Test]
        public void RunCompile_CompilesOncePerModelType()
        {
            var factory = new CountingCompilerServiceFactory();
            using (var service = RazorEngineService.Create(new TemplateServiceConfiguration { CompilerServiceFactory = factory }))
            {
                service.AddTemplate("models", "@Model.GetType().Name");

                var results = RunInParallel(i => i % 2 == 0
                    ? service.RunCompile("models", typeof(Person), new Person())
                    : service.RunCompile("models", typeof(Animal), new Animal()));

                Assert.That(factory.Compiles, Is.EqualTo(2));
                Assert.That(results, Is.EqualTo(Enumerable.Range(0, Threads).Select(i => i % 2 == 0 ? "Person" : "Animal")));
            }
        }

        private static int AssemblyResolveHandlers()
        {
            var field = typeof(AppDomain).GetField("_AssemblyResolve", BindingFlags.NonPublic | BindingFlags.Instance);
            var handlers = (Delegate)field.GetValue(AppDomain.CurrentDomain);
            return handlers == null ? 0 : handlers.GetInvocationList().Length;
        }

        /// <summary>
        /// A disposed <see cref="DefaultCachingProvider"/> leaves no <see cref="AppDomain.AssemblyResolve"/> handler behind.
        /// Its parameterless constructor used to create a second, never disposed inner provider, so every
        /// <see cref="TemplateServiceConfiguration"/> kept a handler and its objects alive for the life of the process.
        /// </summary>
        [Test]
        public void DefaultCachingProvider_LeavesNoAssemblyResolveHandler()
        {
            var before = AssemblyResolveHandlers();

            for (var i = 0; i < 10; i++)
            {
                new DefaultCachingProvider().Dispose();
            }

            Assert.That(AssemblyResolveHandlers(), Is.EqualTo(before));
        }
    }
}
