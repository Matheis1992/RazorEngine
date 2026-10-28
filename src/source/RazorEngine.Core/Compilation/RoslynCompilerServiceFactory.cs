using RazorEngine.Compilation;
using System;

namespace RazorEngine.Roslyn
{
    /// <summary>
    /// Provides a implementation of <see cref="ICompilerServiceFactory"/> for the Roslyn implementation.
    /// Roslyn is the default since version 4, so this is the same as <see cref="DefaultCompilerServiceFactory"/>;
    /// it is kept for existing configurations.
    /// </summary>
    [Serializable]
    public class RoslynCompilerServiceFactory : DefaultCompilerServiceFactory
    {
    }
}
