using Microsoft.CodeAnalysis.CSharp;
using RazorEngine.Compilation;
using RazorEngine.Roslyn.CSharp;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Security;

namespace RazorEngine.Roslyn
{
    /// <summary>
    /// Provides an implementation of <see cref="ICompilerServiceFactory"/> that compiles templates in-process with Roslyn,
    /// so templates can use modern C#. Set it as the compiler service factory of the configuration:
    /// <code>config.CompilerServiceFactory = new RoslynCompilerServiceFactory();</code>
    /// or in a configuration file: <c>compilerServiceFactoryType="RazorEngine.Roslyn.RoslynCompilerServiceFactory, RazorEngine.NetFramework.Roslyn"</c>.
    /// Without it, RazorEngine compiles templates with CodeDom (<see cref="DefaultCompilerServiceFactory"/>).
    /// </summary>
    [Serializable]
    public class RoslynCompilerServiceFactory : ICompilerServiceFactory
    {
        #region Properties
        /// <summary>
        /// The C# language version templates are compiled with.
        /// <see cref="LanguageVersion.Default"/> (the default) is the newest version the bundled Roslyn supports.
        /// Pin an older version to keep the meaning of existing templates when a newer C# version changes it,
        /// for example <see cref="LanguageVersion.CSharp13"/>, where <c>field</c> inside a property accessor is not yet a keyword:
        /// <code>config.CompilerServiceFactory = new RoslynCompilerServiceFactory { LanguageVersion = LanguageVersion.CSharp13 };</code>
        /// </summary>
        public LanguageVersion LanguageVersion { [SecuritySafeCritical] get; [SecuritySafeCritical] set; }
        #endregion

        #region Methods
        /// <summary>
        /// Creates a <see cref="ICompilerService"/> that supports the specified language.
        /// </summary>
        /// <param name="language">The <see cref="Language"/>.</param>
        /// <returns>An instance of <see cref="ICompilerService"/>.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        [SecuritySafeCritical]
        public ICompilerService CreateCompilerService(Language language)
        {
            switch (language)
            {
                case Language.CSharp:
                    return new CSharpRoslynCompilerService { LanguageVersion = LanguageVersion };

                default:
                    throw new ArgumentException("Unsupported language: " + language);
            }
        }
        #endregion
    }
}
