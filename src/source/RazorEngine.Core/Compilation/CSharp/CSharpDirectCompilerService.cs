namespace RazorEngine.Compilation.CSharp
{
    using Microsoft.CSharp;
    using Microsoft.CSharp.RuntimeBinder;
    using RazorEngine.Compilation.ReferenceResolver;
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Security;
    using System.Web.Razor.Parser;

    /// <summary>
    /// Defines a direct compiler service for the C# syntax.
    /// </summary>
    public class CSharpDirectCompilerService : DirectCompilerServiceBase
    {
        #region Constructor
        /// <summary>
        /// Initialises a new instance of <see cref="CSharpDirectCompilerService"/>.
        /// </summary>
        /// <param name="strictMode">Specifies whether the strict mode parsing is enabled.</param>
        /// <param name="markupParserFactory">The markup parser factory to use.</param>
        [SecurityCritical]
        public CSharpDirectCompilerService(bool strictMode = true, Func<ParserBase> markupParserFactory = null)
            : base(
                new CSharpRazorCodeLanguage(strictMode),
                new CSharpCodeProvider(),
                markupParserFactory) { }

        /// <summary>
        /// Initialises a new instance of <see cref="CSharpDirectCompilerService"/> that compiles with the given <see cref="CodeDomProvider"/>
        /// instead of the C# 5 compiler of .NET Framework. For example, <c>CodeDomProvider.CreateProvider("cs")</c> uses the provider
        /// configured in <c>&lt;system.codedom&gt;</c>, such as Microsoft.CodeDom.Providers.DotNetCompilerPlatform for modern C#.
        /// The compiler service owns the provider and disposes it when the service is disposed.
        /// </summary>
        /// <param name="codeDomProvider">The C# <see cref="CodeDomProvider"/> to compile with.</param>
        /// <param name="strictMode">Specifies whether the strict mode parsing is enabled.</param>
        /// <param name="markupParserFactory">The markup parser factory to use.</param>
        [SecurityCritical]
        public CSharpDirectCompilerService(CodeDomProvider codeDomProvider, bool strictMode = true, Func<ParserBase> markupParserFactory = null)
            : base(
                new CSharpRazorCodeLanguage(strictMode),
                codeDomProvider,
                markupParserFactory) { }
        #endregion

        /// <summary>
        /// Extension of a source file without dot ("cs" for C# files or "vb" for VB.NET files).
        /// </summary>
        public override string SourceFileExtension
        {
            get
            {
                return "cs";
            }
        }

        #region Methods
        /// <summary>
        /// Returns a set of assemblies that must be referenced by the compiled template.
        /// </summary>
        /// <returns>The set of assemblies.</returns>
        public override IEnumerable<CompilerReference> IncludeReferences()
        {
            // Ensure the Microsoft.CSharp assembly is referenced to support dynamic typing.
            return new[] { CompilerReference.From(typeof(Binder).Assembly) };
        }


        /// <summary>
        /// Builds a type name for the specified template type.
        /// </summary>
        /// <param name="templateType">The template type.</param>
        /// <param name="modelType">The model type.</param>
        /// <returns>The string type name (including namespace).</returns>
        [Pure]
        public override string BuildTypeName(Type templateType, Type modelType)
        {
            if (templateType == null)
                throw new ArgumentNullException("templateType");

            var modelTypeName = CompilerServicesUtility.ResolveCSharpTypeName(modelType);
            return CompilerServicesUtility.CSharpCreateGenericType(templateType, modelTypeName, false);
        }

        #endregion
    }
}