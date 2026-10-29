using RazorEngine.Templating;
using System;

namespace RazorEngine.Tests.TestTypes
{
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
