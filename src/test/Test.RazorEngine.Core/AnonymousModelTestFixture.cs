using Microsoft.CSharp.RuntimeBinder;
using NUnit.Framework;
using RazorEngine.Templating;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RazorEngine.Tests
{
    /// <summary>
    /// Tests anonymous models, which RazorEngine wraps in a <see cref="Compilation.RazorDynamicObject"/>: calls that the DLR cannot bind
    /// (members of generic types over anonymous types, such as the enumerator of a list of anonymous objects) go straight to the
    /// reflection fallback after the first time, without a <see cref="RuntimeBinderException"/> per call.
    /// </summary>
    [TestFixture]
    public class AnonymousModelTestFixture
    {
        private const string ListTemplate = "@foreach (var item in Model.Items) {<li>@item.Name=@item.Price</li>}@Model.Items.Count";

        private static object CreateListModel(int count)
        {
            return new { Items = Enumerable.Range(0, count).Select(i => new { Name = "n" + i, Price = i }).ToList() };
        }

        /// <summary>
        /// Runs <paramref name="action"/> and returns how many <see cref="RuntimeBinderException"/>s were thrown (and caught) meanwhile.
        /// </summary>
        private static int CountRuntimeBinderExceptions(Action action)
        {
            var count = 0;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, e) =>
            {
                if (e.Exception is RuntimeBinderException)
                {
                    count++;
                }
            };
            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                action();
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }
            return count;
        }

        /// <summary>
        /// A list of anonymous objects renders correctly, and after the first run the enumeration throws no
        /// <see cref="RuntimeBinderException"/> any more (before: three per item, for MoveNext and Current).
        /// </summary>
        [Test]
        public void ListOfAnonymousObjects_RendersWithoutBinderExceptions()
        {
            using (var service = RazorEngineService.Create())
            {
                service.Compile(ListTemplate, "anonymous-list", null);
                Assert.That(service.Run("anonymous-list", null, CreateListModel(2)), Is.EqualTo("<li>n0=0</li><li>n1=1</li>2"));

                string result = null;
                var exceptions = CountRuntimeBinderExceptions(() => result = service.Run("anonymous-list", null, CreateListModel(100)));

                Assert.That(result, Does.StartWith("<li>n0=0</li><li>n1=1</li>").And.EndWith("<li>n99=99</li>100"));
                Assert.That(exceptions, Is.EqualTo(0));
            }
        }

        /// <summary>
        /// A missing member still fails on every run; only calls whose reflection fallback works are remembered.
        /// </summary>
        [Test]
        public void MissingMember_StillThrowsOnEveryRun()
        {
            using (var service = RazorEngineService.Create())
            {
                service.Compile("@Model.Missing", "anonymous-missing", null);
                service.Compile("@Model.Name", "anonymous-present", null);
                var model = new { Name = "n" };

                Assert.Throws<RuntimeBinderException>(() => service.Run("anonymous-missing", null, model));
                Assert.Throws<RuntimeBinderException>(() => service.Run("anonymous-missing", null, model));
                Assert.That(service.Run("anonymous-present", null, model), Is.EqualTo("n"));
            }
        }

        /// <summary>
        /// Members of dynamic objects (here <see cref="System.Dynamic.ExpandoObject"/>) depend on the instance, so they are never remembered:
        /// an expando without a Count entry falls back to the Count of its entries, the next one with a Count entry still shows that value.
        /// </summary>
        [Test]
        public void ExpandoObjects_AreResolvedPerInstance()
        {
            using (var service = RazorEngineService.Create())
            {
                service.Compile("@foreach (var row in Model.Rows) {<i>@row.Count</i>}", "anonymous-expandos", null);
                dynamic withoutCount = new System.Dynamic.ExpandoObject();
                withoutCount.Name = "a";
                dynamic withCount = new System.Dynamic.ExpandoObject();
                withCount.Name = "b";
                withCount.Count = 42;
                var model = new { Rows = new List<System.Dynamic.ExpandoObject> { withoutCount, withCount } };

                for (var i = 0; i < 2; i++)
                {
                    Assert.That(service.Run("anonymous-expandos", null, model), Is.EqualTo("<i>1</i><i>42</i>"));
                }
            }
        }

        /// <summary>
        /// An object inside an anonymous model, wrapped like the model.
        /// </summary>
        public class Formatter
        {
            public string Describe(string value)
            {
                return value ?? "nothing";
            }
        }

        /// <summary>
        /// A null argument is passed on instead of failing with a <see cref="NullReferenceException"/> before the call is bound.
        /// </summary>
        [Test]
        public void NullArgument_IsPassedOn()
        {
            using (var service = RazorEngineService.Create())
            {
                service.Compile("@Model.Formatter.Describe(null) @Model.Formatter.Describe(\"text\")", "anonymous-null-argument", null);

                Assert.That(service.Run("anonymous-null-argument", null, new { Formatter = new Formatter() }), Is.EqualTo("nothing text"));
            }
        }

        /// <summary>
        /// The same member name on different anonymous types is resolved for each type separately.
        /// </summary>
        [Test]
        public void SameMemberOnDifferentTypes_ResolvesPerType()
        {
            using (var service = RazorEngineService.Create())
            {
                service.Compile("@foreach (var item in Model.Items) {<i>@item.Value</i>}", "anonymous-types", null);
                var strings = new { Items = new[] { new { Value = "a" }, new { Value = "b" } }.ToList() };
                var numbers = new { Items = new[] { new { Value = 1 }, new { Value = 2 } }.ToList() };

                for (var i = 0; i < 2; i++)
                {
                    Assert.That(service.Run("anonymous-types", null, strings), Is.EqualTo("<i>a</i><i>b</i>"));
                    Assert.That(service.Run("anonymous-types", null, numbers), Is.EqualTo("<i>1</i><i>2</i>"));
                }
            }
        }
    }
}
