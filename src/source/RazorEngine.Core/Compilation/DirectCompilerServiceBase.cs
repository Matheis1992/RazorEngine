namespace RazorEngine.Compilation
{
    using System;
    using System.CodeDom.Compiler;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Web.Razor;
    using System.Web.Razor.Parser;
    using Templating;

    /// <summary>
    /// Provides a base implementation of a direct compiler service.
    /// </summary>
    public abstract class DirectCompilerServiceBase : CompilerServiceBase
    {
        #region Fields
        private readonly CodeDomProvider _codeDomProvider;
        private bool _disposed;
        #endregion

        #region Constructor
        /// <summary>
        /// Initialises a new instance of <see cref="DirectCompilerServiceBase"/>.
        /// </summary>
        /// <param name="codeLanguage">The razor code language.</param>
        /// <param name="codeDomProvider">The code dom provider used to generate and compile the code.
        /// The compiler service owns it and disposes it when the service is disposed.</param>
        /// <param name="markupParserFactory">The markup parser factory.</param>
        [SecurityCritical]
        protected DirectCompilerServiceBase(RazorCodeLanguage codeLanguage, CodeDomProvider codeDomProvider, Func<ParserBase> markupParserFactory)
            : base(codeLanguage, new ParserBaseCreator(markupParserFactory))
        {
            if (codeDomProvider == null)
                throw new ArgumentNullException("codeDomProvider");

            _codeDomProvider = codeDomProvider;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The underlaying CodeDomProvider instance.
        /// </summary>
        public virtual CodeDomProvider CodeDomProvider { get { return _codeDomProvider; } }

        /// <summary>
        /// Additional compiler switches, appended to the ones RazorEngine sets
        /// (<c>/target:library /optimize /define:RAZORENGINE</c>), for example <c>/define:MYSYMBOL</c>.
        /// Switches like <c>/langversion:7.3</c> need a provider that supports them; the default C# 5 compiler rejects them.
        /// Note that <see cref="CodeDomProvider.CreateProvider(string)"/> does not apply the <c>compilerOptions</c>
        /// of the <c>&lt;system.codedom&gt;</c> configuration section; set them here instead.
        /// </summary>
        public string CompilerOptions { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Called with the compiler parameters of every compile, after RazorEngine has set them up completely
        /// (including the output assembly in the temporary directory) and right before the compiler runs.
        /// Override it to adjust the parameters, for example the <see cref="CompilerParameters.WarningLevel"/>.
        /// </summary>
        /// <param name="parameters">The compiler parameters.</param>
        protected virtual void ConfigureCompilerParameters(CompilerParameters parameters)
        {
        }

        /// <summary>
        /// Creates the compile results for the specified <see cref="TypeContext"/>.
        /// </summary>
        /// <param name="context">The type context.</param>
        /// <returns>The compiler results.</returns>
        [SecurityCritical]
        private Tuple<CompilerResults, string> Compile(TypeContext context)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            var sourceCode = GetCodeCompileUnit(context);
            var assemblies = GetAllReferences(context);

            var fileAssemblies = assemblies
                .Select(a => a.GetFile(
                    msg => new ArgumentException(
                        string.Format(
                            "Unsupported CompilerReference given to CodeDom compiler (only references which can be resolved to files are supported: {0})!",
                            msg))))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
            var haveMscorlib = fileAssemblies.Any(a => a.Contains("mscorlib.dll"));

            var @params = new CompilerParameters
            {
                GenerateInMemory = false,
                GenerateExecutable = false,
                IncludeDebugInformation = Debug,
                TreatWarningsAsErrors = false,
                TempFiles = new TempFileCollection(GetTemporaryDirectory(), true),
                CompilerOptions =
                    string.Format("/target:library /optimize /define:RAZORENGINE {0} {1}",
                        haveMscorlib ? "/nostdlib" : "",
                        CompilerOptions ?? "").Trim()
            };


            @params.ReferencedAssemblies.AddRange(fileAssemblies);
            var tempDir = @params.TempFiles.TempDir;
            var assemblyName = Path.Combine(tempDir,
                String.Format("{0}.dll", GetAssemblyName(context)));
            @params.TempFiles.AddFile(assemblyName, true);
            @params.OutputAssembly = assemblyName;
            ConfigureCompilerParameters(@params);

            var results = _codeDomProvider.CompileAssemblyFromSource(@params, new [] { sourceCode });
            if (Debug)
            {
                bool written = false;
                var targetFile = Path.Combine(results.TempFiles.TempDir, "generated_template." + SourceFileExtension);
                if (!written && !File.Exists(targetFile))
                {
                    File.WriteAllText(targetFile, sourceCode);
                    written = true;
                }
                if (!written)
                {
                    foreach (string item in results.TempFiles)
                    {
                        if (item.EndsWith("." + this.SourceFileExtension))
                        {
                            File.Copy(item, targetFile, true);
                            written = true;
                            break;
                        }
                    } 
                }
            }
            return Tuple.Create(results, sourceCode);
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
            return GenerateSourceCode(CodeDomProvider, results);
        }

        [SecurityCritical]
        private Tuple<Type, CompilationData> CompileTypeImpl(TypeContext context)
        {
            var result = Compile(context);
            var compileResult = result.Item1;

            CompilationData tmpDir;
            if (compileResult.TempFiles != null)
            {
                tmpDir = new CompilationData(result.Item2, compileResult.TempFiles.TempDir);
            }
            else
            {
                tmpDir = new CompilationData(result.Item2, null);
            }

            if (compileResult.Errors != null && compileResult.Errors.HasErrors)
            {
                throw new TemplateCompilationException(
                    compileResult.Errors
                    .Cast<CompilerError>()
                    .Select(error =>
                        new RazorEngineCompilerError(
                            error.ErrorText,
                            error.FileName,
                            error.Line,
                            error.Column,
                            error.ErrorNumber,
                            error.IsWarning)),
                    tmpDir, context.TemplateContent);
            }
            // Make sure we load the assembly from a file and not with
            // Load(byte[]) (or it will be fully trusted!)
            var assemblyPath = compileResult.PathToAssembly;
            if (DisableTempFileLocking)
            {
                compileResult.CompiledAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            }
            else
            {
                compileResult.CompiledAssembly = Assembly.LoadFile(assemblyPath);
            }
            var type = compileResult.CompiledAssembly.GetType(DynamicTemplateNamespace + "." + context.ClassName);
            if (type == null)
            {
                try
                {
                    compileResult.CompiledAssembly.GetTypes();
                }
                catch (Exception e)
                {
                    throw new TemplateLoadingException("Unable to load types of the laded assembly", e);
                }
                // if we are here we just throw
                throw new TemplateLoadingException("We could not find the type in the compiled assembly!");
            }
            return Tuple.Create(type, tmpDir);
        }

        /// <summary>
        /// Compiles the type defined in the specified type context.
        /// </summary>
        /// <param name="context">The type context which defines the type to compile.</param>
        /// <returns>The compiled type.</returns>
        [SecurityCritical]
        public override Tuple<Type, CompilationData> CompileType(TypeContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            (new PermissionSet(PermissionState.Unrestricted)).Assert();
            // Compile as the process identity, not as an impersonated user of the calling thread
            // (csc.exe runs with the process token). Undo restores the impersonation afterwards.
            WindowsImpersonationContext wic = WindowsIdentity.Impersonate(IntPtr.Zero);
            try
            {
                return CompileTypeImpl(context);
            }
            finally
            {
                wic.Undo();
            }
        }

        /// <summary>
        /// Releases managed resources used by this instance.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _codeDomProvider.Dispose();
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}