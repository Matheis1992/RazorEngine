using RazorEngine.Compilation;
using System;

namespace RazorEngine.Roslyn
{
    /// <summary>
    /// Provides a implementation of <see cref="ICompilerServiceFactory"/> that always compiles templates with Roslyn.
    /// Today it does the same as <see cref="DefaultCompilerServiceFactory"/>, but use this factory if your templates rely on Roslyn
    /// (modern C#, <see cref="DefaultCompilerServiceFactory.LanguageVersion"/>): the default compiler may change in a future major version,
    /// this factory will keep compiling with Roslyn.
    /// </summary>
    [Serializable]
    public class RoslynCompilerServiceFactory : DefaultCompilerServiceFactory
    {
    }
}
