namespace RazorEngine.Compilation
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using CSharp;
    using System.Security;

    /// <summary>
    /// Provides a <see cref="ICompilerServiceFactory"/> that compiles templates with CodeDom
    /// (the <c>csc.exe</c> of .NET Framework, C# 5), in a separate compiler process.
    /// This was the default before version 4; use it if you depend on CodeDom specific behaviour.
    /// </summary>
    [Serializable]
    public class CodeDomCompilerServiceFactory : ICompilerServiceFactory
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
                    return new CSharpDirectCompilerService();

                default:
                    throw new ArgumentException("Unsupported language: " + language);
            }
        }
        #endregion
    }
}
