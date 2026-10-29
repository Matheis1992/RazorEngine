using RazorEngine.Compilation.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Security;

namespace RazorEngine.Compilation
{
    /// <summary>
    /// Provides a <see cref="ICompilerServiceFactory"/> that compiles templates with CodeDom, in a separate compiler process.
    /// By default this is the <c>csc.exe</c> of .NET Framework (C# 5). Set <see cref="CodeDomProviderFactory"/> to compile with
    /// another <see cref="CodeDomProvider"/>, for example Microsoft.CodeDom.Providers.DotNetCompilerPlatform for modern C#.
    /// </summary>
    [Serializable]
    public class CodeDomCompilerServiceFactory : ICompilerServiceFactory
    {
        #region Properties
        /// <summary>
        /// Creates the <see cref="CodeDomProvider"/> for each compiler service, or <c>null</c> (the default) for the C# 5 compiler of .NET Framework.
        /// Every call must return a new provider: the compiler service owns it and disposes it.
        /// For example <c>() =&gt; CodeDomProvider.CreateProvider("cs")</c> uses the provider configured in <c>&lt;system.codedom&gt;</c>.
        /// If the factory is passed to another <see cref="AppDomain"/> (for example by an <c>IConfigCreator</c> of
        /// <c>IsolatedRazorEngineService</c>), the delegate is serialized too: a lambda that captures variables cannot be serialized,
        /// so create the factory inside that <see cref="AppDomain"/> instead.
        /// </summary>
        public Func<CodeDomProvider> CodeDomProviderFactory { get; set; }

        /// <summary>
        /// Additional compiler switches for every compiler service, for example <c>/define:MYSYMBOL</c>.
        /// Switches like <c>/langversion:7.3</c> need a provider that supports them (see <see cref="CodeDomProviderFactory"/>);
        /// the default C# 5 compiler rejects them.
        /// See <see cref="DirectCompilerServiceBase.CompilerOptions"/>.
        /// </summary>
        public string CompilerOptions { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Creates a <see cref="ICompilerService"/> that supports the specified language.
        /// </summary>
        /// <param name="language">The <see cref="Language"/>.</param>
        /// <returns>An instance of <see cref="ICompilerService"/>.</returns>
        [SecuritySafeCritical]
        public ICompilerService CreateCompilerService(Language language)
        {
            switch (language)
            {
                case Language.CSharp:
                    CodeDomProvider provider = null;
                    if (CodeDomProviderFactory != null)
                    {
                        provider = CodeDomProviderFactory();
                        if (provider == null)
                            throw new InvalidOperationException("CodeDomProviderFactory returned null; it must return a new CodeDomProvider on every call.");
                    }

                    DirectCompilerServiceBase service;
                    try
                    {
                        service = CreateCSharpCompilerService(provider);
                    }
                    catch
                    {
                        // The service owns the provider, but there is no service.
                        if (provider != null)
                            provider.Dispose();
                        throw;
                    }
                    service.CompilerOptions = CompilerOptions;
                    return service;

                default:
                    throw new ArgumentException("Unsupported language: " + language);
            }
        }

        /// <summary>
        /// Creates the C# compiler service. Override it to use your own service, for example a subclass of
        /// <see cref="CSharpDirectCompilerService"/> that overrides <see cref="DirectCompilerServiceBase.ConfigureCompilerParameters"/>.
        /// <see cref="CompilerOptions"/> is set on the returned service afterwards.
        /// </summary>
        /// <param name="codeDomProvider">
        /// The provider from <see cref="CodeDomProviderFactory"/>, or <c>null</c> for the C# 5 compiler of .NET Framework.
        /// The returned service must own it and dispose it.
        /// </param>
        /// <returns>A new compiler service.</returns>
        [SecurityCritical]
        protected virtual DirectCompilerServiceBase CreateCSharpCompilerService(CodeDomProvider codeDomProvider)
        {
            return codeDomProvider == null
                ? new CSharpDirectCompilerService()
                : new CSharpDirectCompilerService(codeDomProvider);
        }
        #endregion
    }
}
