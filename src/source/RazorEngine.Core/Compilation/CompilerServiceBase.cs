namespace RazorEngine.Compilation
{
    using System;
    using System.CodeDom;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Linq;
    using System.Web.Razor;
    using System.Web.Razor.Generator;
    using System.Web.Razor.Parser;
    using System.CodeDom.Compiler;

    using Inspectors;
    using Templating;
    using RazorEngine.Compilation.ReferenceResolver;
    using System.Security;

    /// <summary>
    /// Provides a base implementation of a compiler service.
    /// </summary>
    public abstract class CompilerServiceBase : ICompilerService
    {
        /// <summary>
        /// The namespace for dynamic templates.
        /// </summary>
        protected internal const string DynamicTemplateNamespace = "CompiledRazorTemplates.Dynamic";
        /// <summary>
        /// A prefix for all dynamically created classes.
        /// </summary>
        protected internal const string ClassNamePrefix = "RazorEngine_";

        /// <summary>
        /// This class only exists because we cannot use Func&lt;ParserBase&gt; in non security-critical class.
        /// </summary>
        [SecurityCritical]
        public class ParserBaseCreator
        {
            /// <summary>
            /// The parser creator.
            /// </summary>
            private Func<ParserBase> creator;
            /// <summary>
            /// Create a new ParserBaseCreator instance.
            /// </summary>
            /// <param name="creator">The parser creator.</param>
            public ParserBaseCreator(Func<ParserBase> creator)
            {
                this.creator = creator ?? (() => new HtmlMarkupParser());
            }
            /// <summary>
            /// Execute the given delegate.
            /// </summary>
            /// <returns></returns>
            public ParserBase Create()
            {
                return this.creator();
            }
        }

        #region Constructor
        /// <summary>
        /// Initialises a new instance of <see cref="CompilerServiceBase"/>
        /// </summary>
        /// <param name="codeLanguage">The code language.</param>
        /// <param name="markupParserFactory">The markup parser factory.</param>
        [SecurityCritical]
        protected CompilerServiceBase(RazorCodeLanguage codeLanguage, ParserBaseCreator markupParserFactory)
        {
            Contract.Requires(codeLanguage != null);

            CodeLanguage = codeLanguage;
            MarkupParserFactory = markupParserFactory ?? new ParserBaseCreator(null);
            ReferenceResolver = new UseCurrentAssembliesReferenceResolver();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the set of code inspectors.
        /// </summary>
        [Obsolete("This API is obsolete and will be removed in the next version (code inspection relies on CodeDom code generation, which newer Razor versions no longer use)!")]
        public IEnumerable<ICodeInspector> CodeInspectors { get; set; }

        /// <summary>
        /// Gets or sets the assembly resolver.
        /// </summary>
        public IReferenceResolver ReferenceResolver { get; set; }

        /// <summary>
        /// Gets the code language.
        /// </summary>
        public RazorCodeLanguage CodeLanguage { [SecurityCritical] get; [SecurityCritical] private set; }

        /// <summary>
        /// Gets or sets whether the compiler service is operating in debug mode.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets whether the compiler should load assemblies with Assembly.Load(byte[])
        /// to prevent files from being locked.
        /// </summary>
        public bool DisableTempFileLocking { get; set; }

        /// <summary>
        /// Gets the markup parser.
        /// </summary>
        public ParserBaseCreator MarkupParserFactory { [SecurityCritical] get; [SecurityCritical] private set; }

        /// <summary>
        /// Extension of a source file without dot ("cs" for C# files or "vb" for VB.NET files).
        /// </summary>
        public abstract string SourceFileExtension { get; }

        private bool _disposed;

        #endregion

        #region Methods

        /// <summary>
        /// Tries to create and return a unique temporary directory.
        /// </summary>
        /// <returns>the (already created) temporary directory</returns>
        protected static string GetDefaultTemporaryDirectory()
        {
            var created = false;
            var tried = 0;
            string tempDirectory = "";
            while (!created && tried < 10)
            {
                tried++;
                try
                {
                    tempDirectory = Path.Combine(Path.GetTempPath(), "RazorEngine_" + Path.GetRandomFileName());
                    if (!Directory.Exists(tempDirectory))
                    {
                        Directory.CreateDirectory(tempDirectory);
                        created = Directory.Exists(tempDirectory);
                    }
                }
                catch (IOException)
                {
                    if (tried > 8)
                    {
                        throw;
                    }
                }
            }
            if (!created)
            {
                throw new Exception("Could not create a temporary directory! Maybe all names are already used?");
            }
            return tempDirectory;
        }

        /// <summary>
        /// Returns a new temporary directory ready to be used.
        /// This can be overwritten in subclases to change the created directories.
        /// </summary>
        /// <returns></returns>
        protected virtual string GetTemporaryDirectory()
        {
            return GetDefaultTemporaryDirectory();
        }

        /// <summary>
        /// Builds a type name for the specified template type.
        /// </summary>
        /// <param name="templateType">The template type.</param>
        /// <param name="modelType">The model type.</param>
        /// <returns>The string type name (including namespace).</returns>
        [Pure]
        public abstract string BuildTypeName(Type templateType, Type modelType);

        /// <summary>
        /// Compiles the type defined in the specified type context.
        /// </summary>
        /// <param name="context">The type context which defines the type to compile.</param>
        /// <returns>The compiled type.</returns>
        [SecurityCritical]
        public abstract Tuple<Type, CompilationData> CompileType(TypeContext context);

        /// <summary>
        /// Creates a <see cref="RazorEngineHost"/> used for class generation.
        /// </summary>
        /// <param name="templateType">The template base type.</param>
        /// <param name="modelType">The model type.</param>
        /// <param name="className">The class name.</param>
        /// <returns>An instance of <see cref="RazorEngineHost"/>.</returns>

        [SecurityCritical]
        private RazorEngineHost CreateHost(Type templateType, Type modelType, string className)
        {
            var host =
                new RazorEngineHost(CodeLanguage, MarkupParserFactory.Create)
                {
                    DefaultBaseTemplateType = templateType,
                    DefaultModelType = modelType,
                    DefaultBaseClass = BuildTypeName(templateType, modelType),
                    DefaultClassName = className,
                    DefaultNamespace = DynamicTemplateNamespace,
                    GeneratedClassContext =
                        new GeneratedClassContext(
                            "Execute", "Write", "WriteLiteral", "WriteTo", "WriteLiteralTo",
                            "RazorEngine.Templating.TemplateWriter", "DefineSection"
                        )
                        {
                            ResolveUrlMethodName = "ResolveUrl"
                        }
                };

            return host;
        }


        /// <summary>
        /// Gets the source code from Razor for the given template.
        /// </summary>
        /// <param name="className">The class name.</param>
        /// <param name="template">The template to compile.</param>
        /// <param name="namespaceImports">The set of namespace imports.</param>
        /// <param name="templateType">The template type.</param>
        /// <param name="modelType">The model type.</param>
        /// <returns></returns>
        [Pure][SecurityCritical]
        public string GetCodeCompileUnit(string className, ITemplateSource template, ISet<string> namespaceImports, Type templateType, Type modelType)
        {
            var typeContext =
                new TypeContext(className, namespaceImports)
                {
                    TemplateContent = template,
                    TemplateType = templateType,
                    ModelType = modelType
                };
            return GetCodeCompileUnit(typeContext);
        }

        /// <summary>
        /// Helper method to generate the prefered assembly name.
        /// </summary>
        /// <param name="context">the context of the current compilation.</param>
        /// <returns></returns>
        protected string GetAssemblyName(TypeContext context)
        {
            return String.Format("{0}.{1}", DynamicTemplateNamespace, context.ClassName);
        }

        /// <summary>
        /// Inspects the source and returns the source code.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [SecurityCritical]
        public abstract string InspectSource(GeneratorResults results, TypeContext context);

        /// <summary>
        /// Gets the code compile unit used to compile a type.
        /// </summary>
        /// <param name="context"></param>
        /// <returns>A <see cref="CodeCompileUnit"/> used to compile a type.</returns>
        [Pure][SecurityCritical]
        public string GetCodeCompileUnit(TypeContext context)
        {
            string className = context.ClassName;
            ITemplateSource template = context.TemplateContent;
            ISet<string> namespaceImports = context.Namespaces;
            Type templateType = context.TemplateType;
            Type modelType = context.ModelType;

            if (string.IsNullOrEmpty(className))
                throw new ArgumentException("Class name is required.");

            if (template == null)
                throw new ArgumentException("Template is required.");

            namespaceImports = namespaceImports ?? new HashSet<string>();
            templateType = templateType ?? ((modelType == null) ? typeof(TemplateBase) : typeof(TemplateBase<>));

            // Create the RazorEngineHost
            var host = CreateHost(templateType, modelType, className);

            // Add any required namespace imports
            foreach (string ns in GetNamespaces(templateType, namespaceImports))
                host.NamespaceImports.Add(ns);

            // Gets the generator result.
            return GetGeneratorResult(host, context);
        }

        /// <summary>
        /// Gets the generator result.
        /// </summary>
        /// <param name="host">The razor engine host.</param>
        /// <param name="context">The compile context.</param>
        /// <returns>The generator result.</returns>
        [SecurityCritical]
        private string GetGeneratorResult(RazorEngineHost host, TypeContext context)
        {
            var engine = new RazorTemplateEngine(host);
            GeneratorResults result;
            using (var reader = context.TemplateContent.GetTemplateReader())
                result = engine.GenerateCode(reader, null, null, context.TemplateContent.TemplateFile);
            // Prepared here (not in InspectSource), so every compiler service gets the same template class.
            PrepareGeneratedCode(result, context);
            return InspectSource(result, context);
        }

        /// <summary>
        /// Turns the generated code into source code with the given CodeDom provider.
        /// </summary>
        /// <param name="provider">The provider that writes the source code.</param>
        /// <param name="results">The results of the Razor code generator.</param>
        /// <returns>The source code.</returns>
        [SecurityCritical]
        protected static string GenerateSourceCode(CodeDomProvider provider, GeneratorResults results)
        {
            var builder = new System.Text.StringBuilder();
            using (var writer = new StringWriter(builder, System.Globalization.CultureInfo.InvariantCulture))
            {
                provider.GenerateCodeFromCompileUnit(results.GeneratedCode, writer, new CodeGeneratorOptions());
            }
            return builder.ToString();
        }

        /// <summary>
        /// Gets any required namespace imports.
        /// </summary>
        /// <param name="templateType">The template type.</param>
        /// <param name="otherNamespaces">The requested set of namespace imports.</param>
        /// <returns>A set of namespace imports.</returns>
        private static IEnumerable<string> GetNamespaces(Type templateType, IEnumerable<string> otherNamespaces)
        {
            var templateNamespaces = templateType.GetCustomAttributes(typeof(RequireNamespacesAttribute), true)
                .Cast<RequireNamespacesAttribute>()
                .SelectMany(a => a.Namespaces)
                .Concat(otherNamespaces)
                .Distinct();

            return templateNamespaces;
        }

        /// <summary>
        /// Returns a set of assemblies that must be referenced by the compiled template.
        /// </summary>
        /// <returns>The set of assemblies.</returns>
        [Obsolete("Use IncludeReferences instead")]
        public virtual IEnumerable<string> IncludeAssemblies()
        {
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Returns a set of references that must be referenced by the compiled template.
        /// </summary>
        /// <returns>The set of references.</returns>
        public virtual IEnumerable<CompilerReference> IncludeReferences()
        {
            return Enumerable.Empty<CompilerReference>();
        }

        /// <summary>
        /// Helper method to get all references for the given compilation.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected IEnumerable<CompilerReference> GetAllReferences(TypeContext context)
        {
#pragma warning disable 0618 // Backwards Compat.
            var references =
                ReferenceResolver.GetReferences(
                    context,
                    IncludeAssemblies()
                        .Select(RazorEngine.Compilation.ReferenceResolver.CompilerReference.From)
                        .Concat(IncludeReferences()))
#pragma warning restore 0618 // Backwards Compat.
                .ToList();
            context.AddReferences(references);
            return references;
        }

        /// <summary>
        /// Inspects the generated code compile unit.
        /// </summary>
        /// <param name="unit">The code compile unit.</param>
        [Obsolete("Will be removed in 4.x")]
        protected virtual void Inspect(CodeCompileUnit unit)
        {
            Contract.Requires(unit != null);

            var ns = unit.Namespaces[0];
            var type = ns.Types[0];
            var executeMethod = type.Members.OfType<CodeMemberMethod>().Where(m => m.Name.Equals("Execute")).Single();

            foreach (var inspector in CodeInspectors)
                inspector.Inspect(unit, ns, type, executeMethod);
        }

        /// <summary>
        /// Prepares the generated code before it is turned into source code, so that the CodeDom and the
        /// Roslyn compiler services produce the same template class: makes the template debuggable,
        /// marks dynamic models, adds the constructors required by the base template type and runs the code inspectors.
        /// </summary>
        /// <param name="results">The results of the Razor code generator.</param>
        /// <param name="context">The type context.</param>
        [SecurityCritical]
        private void PrepareGeneratedCode(GeneratorResults results, TypeContext context)
        {
            if (context.TemplateContent.TemplateFile == null)
            {
                // Allow to step into the template code by removing the "#line hidden" pragmas
                foreach (CodeNamespace @namespace in results.GeneratedCode.Namespaces.Cast<CodeNamespace>().ToList())
                {
                    foreach (CodeTypeDeclaration @type in @namespace.Types.Cast<CodeTypeDeclaration>().ToList())
                    {
                        foreach (CodeTypeMember member in @type.Members.Cast<CodeTypeMember>().ToList())
                        {
                            var snippet = member as CodeSnippetTypeMember;
                            if (snippet != null && snippet.Text.Contains("#line hidden"))
                            {
                                snippet.Text = snippet.Text.Replace("#line hidden", "");
                            }
                        }
                    }
                }
            }

            // Add the dynamic model attribute if the type is an anonymous type.
            var generatedType = results.GeneratedCode.Namespaces[0].Types[0];
            if (context.ModelType != null && CompilerServicesUtility.IsDynamicType(context.ModelType))
                generatedType.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof(HasDynamicModelAttribute))));

            // Generate any constructors required by the base template type.
            GenerateConstructors(CompilerServicesUtility.GetConstructors(context.TemplateType), generatedType);

#pragma warning disable 0618 // Backwards Compat.
            // Despatch any inspectors
            Inspect(results.GeneratedCode);
#pragma warning restore 0618 // Backwards Compat.
        }

        /// <summary>
        /// Generates any required contructors for the specified type.
        /// </summary>
        /// <param name="constructors">The set of constructors.</param>
        /// <param name="codeType">The code type declaration.</param>
        [Pure]
        private static void GenerateConstructors(IEnumerable<System.Reflection.ConstructorInfo> constructors, CodeTypeDeclaration codeType)
        {
            if (constructors == null || !constructors.Any())
                return;

            var existingConstructors = codeType.Members.OfType<CodeConstructor>().ToArray();
            foreach (var existingConstructor in existingConstructors)
                codeType.Members.Remove(existingConstructor);

            foreach (var constructor in constructors)
            {
                var ctor = new CodeConstructor();
                ctor.Attributes = MemberAttributes.Public;

                foreach (var param in constructor.GetParameters())
                {
                    ctor.Parameters.Add(new CodeParameterDeclarationExpression(param.ParameterType, param.Name));
                    ctor.BaseConstructorArgs.Add(new CodeSnippetExpression(param.Name));
                }

                codeType.Members.Add(ctor);
            }
        }

        /// <summary>
        /// Disposes the current instance.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Disposes the current instance via the disposable pattern.
        /// </summary>
        /// <param name="disposing">true when Dispose() was called manually.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        #endregion
    }
}