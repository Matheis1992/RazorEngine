using System;

namespace RazorEngine.Compilation
{
    /// <summary>
    /// Provides the default implementation of a <see cref="ICompilerServiceFactory"/>: templates are compiled with CodeDom,
    /// the same as <see cref="CodeDomCompilerServiceFactory"/>. To compile templates in-process with Roslyn (modern C#),
    /// add the RazorEngine.NetFramework.Roslyn package and set <c>RazorEngine.Roslyn.RoslynCompilerServiceFactory</c>.
    /// </summary>
    [Serializable]
    public class DefaultCompilerServiceFactory : CodeDomCompilerServiceFactory
    {
    }
}
