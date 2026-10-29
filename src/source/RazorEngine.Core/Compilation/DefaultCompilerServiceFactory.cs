namespace RazorEngine.Compilation
{
    using Microsoft.CodeAnalysis.CSharp;
    using RazorEngine.Roslyn.CSharp;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Security;

    /// <summary>
    /// Provides a default implementation of a <see cref="ICompilerServiceFactory"/>:
    /// templates are compiled in-process with Roslyn (modern C#).
    /// Use <see cref="CodeDomCompilerServiceFactory"/> to compile templates with CodeDom (C# 5) instead.
    /// </summary>
    [Serializable]
    public class DefaultCompilerServiceFactory : ICompilerServiceFactory
    {
        #region Properties
        /// <summary>
        /// The C# language version templates are compiled with.
        /// <see cref="LanguageVersion.Default"/> (the default) is the newest version the bundled Roslyn supports.
        /// Pin an older version to keep the meaning of existing templates when a newer C# version changes it,
        /// for example <see cref="LanguageVersion.CSharp13"/>, where <c>field</c> inside a property accessor is not yet a keyword:
        /// <code>config.CompilerServiceFactory = new RoslynCompilerServiceFactory { LanguageVersion = LanguageVersion.CSharp13 };</code>
        /// (<see cref="RazorEngine.Roslyn.RoslynCompilerServiceFactory"/> keeps compiling with Roslyn if the default compiler changes.)
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
