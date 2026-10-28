namespace RazorEngine.Compilation
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using System.Security;
    using RazorEngine.Roslyn.CSharp;

    /// <summary>
    /// Provides a default implementation of a <see cref="ICompilerServiceFactory"/>:
    /// templates are compiled in-process with Roslyn (modern C#).
    /// Use <see cref="CodeDomCompilerServiceFactory"/> to compile templates with CodeDom (C# 5) instead.
    /// </summary>
    [Serializable]
    public class DefaultCompilerServiceFactory : ICompilerServiceFactory
    {
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
                    return new CSharpRoslynCompilerService();

                default:
                    throw new ArgumentException("Unsupported language: " + language);
            }
        }
        #endregion
    }
}
