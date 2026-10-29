using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Web.Razor;
using System.Web.Razor.Parser;

namespace RazorEngine.Roslyn.CSharp
{
    /// <summary>
    /// A concrete <see cref="ICompilerService"/> implementation for C# by using the Roslyn compiler.
    /// </summary>
    [SecurityCritical]
    public class CSharpRoslynCompilerService : RoslynCompilerServiceBase
    {
        /// <summary>
        /// We need a CodeDom instance as Razor uses CodeDom
        /// internally and we need to generate the source code file...
        /// </summary>
        private Microsoft.CSharp.CSharpCodeProvider _codeDomProvider; 
        /// <summary>
        /// Creates a new CSharpRoslynCompilerService instance.
        /// </summary>
        /// <param name="strictMode"></param>
        /// <param name="markupParserFactory"></param>
        [SecurityCritical]
        public CSharpRoslynCompilerService(bool strictMode = true, Func<ParserBase> markupParserFactory = null)
            : base(
                new RazorEngine.Compilation.CSharp.CSharpRazorCodeLanguage(strictMode),
                markupParserFactory) {
            _codeDomProvider = new Microsoft.CSharp.CSharpCodeProvider(); 
        }

        /// <summary>
        /// The C# language version templates are compiled with.
        /// <see cref="LanguageVersion.Default"/> (the default) is the newest version this Roslyn release supports.
        /// Set an older version to keep the meaning of existing templates when a newer C# version changes it,
        /// for example <see cref="LanguageVersion.CSharp13"/>, where <c>field</c> inside a property accessor is not yet a keyword.
        /// </summary>
        public LanguageVersion LanguageVersion { get; set; }

        /// <summary>
        /// Returns "cs".
        /// </summary>
        public override string SourceFileExtension
        {
            [SecuritySafeCritical]
            get { return "cs"; }
        }

        /// <summary>
        /// Inspects the GeneratorResults and returns the source code.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [SecurityCritical]
        public override string InspectSource(GeneratorResults results, TypeContext context)
        {
            return GenerateSourceCode(_codeDomProvider, results);
        }

        /// <summary>
        /// Build a C# typename.
        /// </summary>
        /// <param name="templateType"></param>
        /// <param name="modelType"></param>
        /// <returns></returns>
        [SecuritySafeCritical]
        public override string BuildTypeName(Type templateType, Type modelType)
        {
            if (templateType == null)
                throw new ArgumentNullException("templateType");

            var modelTypeName = CompilerServicesUtility.ResolveCSharpTypeName(modelType);
            return CompilerServicesUtility.CSharpCreateGenericType(templateType, modelTypeName, false);
        }

        /// <summary>
        /// Creates a CSharpSyntaxTree instance.
        /// </summary>
        /// <param name="sourceCode"></param>
        /// <param name="sourceCodePath"></param>
        /// <returns></returns>
        [SecurityCritical]
        public override Microsoft.CodeAnalysis.SyntaxTree GetSyntaxTree(string sourceCode, string sourceCodePath)
        {
            // Same symbol as the CodeDom compiler service (/define:RAZORENGINE), so templates can use #if RAZORENGINE.
            var options = CSharpParseOptions.Default
                .WithLanguageVersion(LanguageVersion)
                .WithPreprocessorSymbols("RAZORENGINE");
            return CSharpSyntaxTree.ParseText(sourceCode, options, sourceCodePath, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Creates a CSharpCompilation instance
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        [SecurityCritical]
        public override Microsoft.CodeAnalysis.Compilation GetEmptyCompilation(string assemblyName)
        {
            return CSharpCompilation.Create(assemblyName);
        }
        /// <summary>
        /// Creates a CSharpCompilationOptions instance.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [SecurityCritical]
        public override CompilationOptions CreateOptions(TypeContext context)
        {
            return
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                // Optimized like the CodeDom compiler service (/optimize), also when Debug is set.
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithUsings(context.Namespaces);
        }

        /// <summary>
        /// Returns a set of assemblies that must be referenced by the compiled template.
        /// </summary>
        /// <returns>The set of assemblies.</returns>
        [SecuritySafeCritical]
        public override IEnumerable<CompilerReference> IncludeReferences()
        {
            // Ensure the Microsoft.CSharp assembly is referenced to support dynamic typing.
            return new[] { CompilerReference.From(typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly) };
        }
    }
}
