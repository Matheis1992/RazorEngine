using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using RazorEngine.Compilation;
using RazorEngine.Compilation.ReferenceResolver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Web.Razor;
using System.Web.Razor.Parser;


namespace RazorEngine.Roslyn.CSharp
{
    /// <summary>
    /// Base compiler service class for roslyn compilers
    /// </summary>
    [SecurityCritical]
    public abstract class RoslynCompilerServiceBase : CompilerServiceBase
    {
        /// <summary>
        /// Metadata references of loaded assemblies, shared by all compiles in this AppDomain.
        /// Passing the same reference objects again lets Roslyn reuse the symbols it built for them,
        /// instead of reading and analyzing every referenced assembly on each compile.
        /// A loaded assembly cannot change, so its reference never goes stale, and there is at most one entry per loaded assembly.
        /// References given as a file path, stream or byte array are not cached: their content can change between compiles.
        /// </summary>
        private static readonly ConditionalWeakTable<Assembly, MetadataReference> AssemblyReferences =
            new ConditionalWeakTable<Assembly, MetadataReference>();

        /// <summary>
        /// Converts a <see cref="CompilerReference"/> to a Roslyn <see cref="MetadataReference"/>,
        /// reusing the reference of a loaded assembly from earlier compiles.
        /// </summary>
        [SecurityCritical]
        internal static MetadataReference ToMetadataReference(CompilerReference reference)
        {
            return reference.Visit(new SelectMetadataReference());
        }

        [SecuritySafeCritical]
        private class SelectMetadataReference : CompilerReference.ICompilerReferenceVisitor<MetadataReference>
        {
            [SecuritySafeCritical]
            public MetadataReference Visit(Assembly assembly)
            {
                return AssemblyReferences.GetValue(assembly, a => MetadataReference.CreateFromFile(a.Location));
            }

            [SecuritySafeCritical]
            public MetadataReference Visit(string file)
            {
                return MetadataReference.CreateFromFile(file);
            }

            [SecuritySafeCritical]
            public MetadataReference Visit(Stream stream)
            {
                return MetadataReference.CreateFromStream(stream);
            }

            [SecuritySafeCritical]
            public MetadataReference Visit(byte[] byteArray)
            {
                return MetadataReference.CreateFromImage(byteArray);
            }
        }

        /// <summary>
        /// Required for #line pragmas
        /// </summary>
        [SecurityCritical]
        protected class RazorEngineSourceReferenceResolver : SourceReferenceResolver
        {
            private string _sourceCodeFile;
            /// <summary>
            /// Constructs a new RazorEngineSourceReferenceResolver instance.
            /// </summary>
            /// <param name="sourceCodeFile"></param>
            public RazorEngineSourceReferenceResolver(string sourceCodeFile)
            {
                _sourceCodeFile = sourceCodeFile;
            }

            /// <summary>
            /// Checkts if the current instance equals the given instance.
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            [SecuritySafeCritical]
            public override bool Equals(object other)
            {
                return object.Equals(this, other);
            }

            /// <summary>
            /// Calculates a hashcode for the current instance.
            /// </summary>
            /// <returns></returns>
            [SecuritySafeCritical]
            public override int GetHashCode()
            {
                return 0;
            }

            /// <summary>
            /// Normalize a path
            /// </summary>
            /// <param name="path"></param>
            /// <param name="baseFilePath"></param>
            /// <returns></returns>
            [SecurityCritical]
            public override string NormalizePath(string path, string baseFilePath)
            {
                if (File.Exists(path))
                {
                    return path;
                }

                if (string.IsNullOrEmpty(baseFilePath))
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        return _sourceCodeFile;
                    }
                    return path;
                }
                else
                {
                    return baseFilePath;
                }
            }

            /// <summary>
            /// Open a path for reading
            /// </summary>
            /// <param name="resolvedPath"></param>
            /// <returns></returns>
            [SecurityCritical]
            public override Stream OpenRead(string resolvedPath)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Resolve a reference.
            /// </summary>
            /// <param name="path"></param>
            /// <param name="baseFilePath"></param>
            /// <returns></returns>
            [SecurityCritical]
            public override string ResolveReference(string path, string baseFilePath)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="RoslynCompilerServiceBase"/> class.
        /// </summary>
        /// <param name="codeLanguage"></param>
        /// <param name="markupParserFactory"></param>
        [SecuritySafeCritical]
        public RoslynCompilerServiceBase(RazorCodeLanguage codeLanguage, Func<ParserBase> markupParserFactory)
            : base(codeLanguage, new ParserBaseCreator(markupParserFactory))
        {

        }

        /// <summary>
        /// Get a new empty compilation instance from the concrete implementation.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public abstract Microsoft.CodeAnalysis.Compilation GetEmptyCompilation(string assemblyName);

        /// <summary>
        /// Gets a SyntaxTree from the given source code.
        /// </summary>
        /// <param name="sourceCode"></param>
        /// <param name="sourceCodeFile"></param>
        /// <returns></returns>
        public abstract SyntaxTree GetSyntaxTree(string sourceCode, string sourceCodeFile);

        /// <summary>
        /// Create a empty CompilationOptions with the given namespace usings.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract CompilationOptions CreateOptions(TypeContext context);

        /// <summary>
        /// Configures and runs the compiler.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [SecurityCritical]
        public override Tuple<Type, CompilationData> CompileType(TypeContext context)
        {
            var sourceCode = GetCodeCompileUnit(context);
            var assemblyName = GetAssemblyName(context);

            (new PermissionSet(PermissionState.Unrestricted)).Assert();
            var tempDir = GetTemporaryDirectory();

            var sourceCodeFile = Path.Combine(tempDir, String.Format("{0}.{1}", assemblyName, SourceFileExtension));
            File.WriteAllText(sourceCodeFile, sourceCode);

            var references = GetAllReferences(context);

            var compilation =
                GetEmptyCompilation(assemblyName)
                .AddSyntaxTrees(
                    GetSyntaxTree(sourceCode, sourceCodeFile))
                .AddReferences(GetMetadataReferences(references));

            compilation =
                compilation
                .WithOptions(
                    CreateOptions(context)
                    .WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
                    .WithPlatform(Platform.AnyCpu)
                    .WithSourceReferenceResolver(new RazorEngineSourceReferenceResolver(sourceCodeFile)));

            var assemblyFile = Path.Combine(tempDir, String.Format("{0}.dll", assemblyName));

            var assemblyPdbFile = Path.Combine(tempDir, String.Format("{0}.pdb", assemblyName));
            var compilationData = new CompilationData(sourceCode, tempDir);

            using (var assemblyStream = File.Open(assemblyFile, FileMode.Create, FileAccess.ReadWrite))
            using (var pdbStream = File.Open(assemblyPdbFile, FileMode.Create, FileAccess.ReadWrite))
            {
                var opts = new EmitOptions()
                    .WithPdbFilePath(assemblyPdbFile);
                var pdbStreamHelper = pdbStream;

                var result = compilation.Emit(assemblyStream, pdbStreamHelper, options: opts);
                if (!result.Success)
                {
                    var errors =
                        result.Diagnostics.Select(diag =>
                        {
                            var lineSpan = diag.Location.GetLineSpan();
                            return new Templating.RazorEngineCompilerError(
                                string.Format("{0}", diag.GetMessage()),
                                lineSpan.Path,
                                lineSpan.StartLinePosition.Line,
                                lineSpan.StartLinePosition.Character,
                                diag.Id,
                                diag.Severity != DiagnosticSeverity.Error);
                        });

                    throw new Templating.TemplateCompilationException(errors, compilationData, context.TemplateContent);
                }
            }

            // load file and return loaded type.
            Assembly assembly;
            if (DisableTempFileLocking)
            {
                assembly = File.Exists(assemblyPdbFile)
                    ? Assembly.Load(File.ReadAllBytes(assemblyFile), File.ReadAllBytes(assemblyPdbFile))
                    : Assembly.Load(File.ReadAllBytes(assemblyFile));
            }
            else
            {
                assembly = Assembly.LoadFrom(assemblyFile);
            }
            var type = assembly.GetType(DynamicTemplateNamespace + "." + context.ClassName);
            return Tuple.Create(type, compilationData);
        }

        [SecurityCritical]
        private MetadataReference[] GetMetadataReferences(IEnumerable<CompilerReference> references)
        {
            return references.Select(ToMetadataReference).ToArray();
        }
    }
}
