namespace RazorEngine.Tests.TestTypes
{
    using RazorEngine.Templating;
    using System;

    /// <summary>
    /// Test class.
    /// </summary>
    public class InlineTemplateModel
    {
        /// <summary>
        /// Test class.
        /// </summary>
        public Func<dynamic, TemplateWriter> InlineTemplate { get; set; }
    }
}
