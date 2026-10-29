using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RazorEngine.Compilation.ReferenceResolver
{
    /// <summary>
    /// Resolves the assemblies by using all currently loaded assemblies. See <see cref="IReferenceResolver"/>
    /// </summary>
    public class UseCurrentAssembliesReferenceResolver : IReferenceResolver
    {
        /// <summary>
        /// Assemblies of the original RazorEngine package. They define the same RazorEngine.* types as this assembly,
        /// so passing them to the compiler would make every template fail with ambiguous types (CS0433)
        /// when both packages are loaded in the same process.
        /// </summary>
        private static readonly HashSet<string> ConflictingAssemblies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RazorEngine", "RazorEngine.Roslyn" };

        /// <summary>
        /// See <see cref="IReferenceResolver.GetReferences"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="includeAssemblies"></param>
        /// <returns></returns>
        public IEnumerable<CompilerReference> GetReferences(TypeContext context = null, IEnumerable<CompilerReference> includeAssemblies = null)
        {
            return CompilerServicesUtility
                   .GetLoadedAssemblies()
                   .Where(a => !a.IsDynamic && File.Exists(a.Location) && !a.Location.Contains(CompilerServiceBase.DynamicTemplateNamespace))
                   .Where(a => !ConflictingAssemblies.Contains(a.GetName().Name))
                   .GroupBy(a => a.GetName().Name).Select(grp => grp.First(y => y.GetName().Version == grp.Max(x => x.GetName().Version))) // only select distinct assemblies based on FullName to avoid loading duplicate assemblies
                   .Select(a => CompilerReference.From(a))
                   .Concat(includeAssemblies ?? Enumerable.Empty<CompilerReference>());
        }
    }
}
