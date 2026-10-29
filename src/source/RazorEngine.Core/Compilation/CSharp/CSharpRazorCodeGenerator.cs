using RazorEngine.Templating;
using System.Security;
using System.Web.Razor.Parser.SyntaxTree;
using OriginalCSharpRazorCodeGenerator = System.Web.Razor.Generator.CSharpRazorCodeGenerator;
using OriginalRazorEngineHost = System.Web.Razor.RazorEngineHost;

namespace RazorEngine.Compilation.CSharp
{
    /// <summary>
    /// Defines a code generator that supports C# syntax.
    /// </summary>
    [SecurityCritical]
    public class CSharpRazorCodeGenerator : OriginalCSharpRazorCodeGenerator
    {
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="CSharpRazorCodeGenerator"/> class.
        /// </summary>
        /// <param name="className">Name of the class.</param>
        /// <param name="rootNamespaceName">Name of the root namespace.</param>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="host">The host.</param>
        /// <param name="strictMode">Flag to specify that this generator is running in struct mode.</param>
        public CSharpRazorCodeGenerator(string className, string rootNamespaceName, string sourceFileName, OriginalRazorEngineHost host, bool strictMode)
            : base(className, rootNamespaceName, sourceFileName, host)
        {
            StrictMode = strictMode;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Gets whether the code generator is running in strict mode.
        /// </summary>
        public bool StrictMode { get; private set; }
        #endregion

        #region Methods
        /// <summary>
        /// Visits an error generated through parsing.
        /// </summary>
        /// <param name="err">The error that was generated.</param>
        [SecurityCritical]
        public override void VisitError(RazorError err)
        {
            if (StrictMode)
                throw new TemplateParsingException(err.Message, err.Location.CharacterIndex, err.Location.LineIndex);
        }
        #endregion
    }
}