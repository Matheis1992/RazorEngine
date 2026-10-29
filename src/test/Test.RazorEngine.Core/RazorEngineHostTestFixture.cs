namespace RazorEngine.Tests
{
    using Compilation;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.IO;
    using Templating;
    using TestTypes;

    /// <summary>
    /// Defines a test fixture that provides tests for the <see cref="TemplateBase"/> type.
    /// </summary>
    [TestFixture]
    public class RazorEngineHostTestFixture
    {
        #region Tests
        /// <summary>
        /// Tests that the <see cref="RazorEngineHost"/> supports the @model directive.
        /// </summary>
        /// <remarks>
        /// As with it's MVC counterpart, we've added support for the @model declaration. This is to enable scenarios
        /// where the model type might be unknown, but we can pass in an instance of <see cref="object" /> and allow
        /// the @model directive to switch the model type.
        /// </remarks>
        [Test]
        public void RazorEngineHost_SupportsModelSpan_UsingCSharpCodeParser()
        {
            using (var service = RazorEngineService.Create())
            using (var writer = new StringWriter())
            {
                const string template = "@model List<RazorEngine.Tests.TestTypes.Person>\n@Model.Count";
                const string expected = "1";

                var model = new List<Person> { new Person() { Forename = "Matt", Age = 27 } };
                var key = service.GetKey(nameof(template));

                service.AddTemplate(key, template);
                service.RunCompile(key, writer, modelType: model.GetType(), model: model);

                string result = writer.ToString();

                Assert.That(result == expected, "Result does not match expected: " + result);
            }
        }
        #endregion
    }
}