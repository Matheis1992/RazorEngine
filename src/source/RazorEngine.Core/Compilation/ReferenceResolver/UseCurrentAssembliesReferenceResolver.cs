using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        /// The name of each loaded assembly that can be passed to the compiler, or null if it cannot. Checked once per assembly
        /// instead of on every compile: <see cref="File.Exists"/> costs about 60 µs per file, which made every compile in a process
        /// with 200 loaded assemblies about 15 ms slower. The file of a loaded assembly is locked, so it does not disappear.
        /// </summary>
        private static readonly ConditionalWeakTable<Assembly, AssemblyName> ReferenceableNames =
            new ConditionalWeakTable<Assembly, AssemblyName>();

        private static AssemblyName GetReferenceableName(Assembly assembly)
        {
            var name = assembly.GetName();
            var canBeReferenced =
                !assembly.IsDynamic
                && !assembly.Location.Contains(CompilerServiceBase.DynamicTemplateNamespace)
                && !ConflictingAssemblies.Contains(name.Name)
                && File.Exists(assembly.Location);
            return canBeReferenced ? name : null;
        }

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
                   .Select(a => new { Assembly = a, Name = ReferenceableNames.GetValue(a, GetReferenceableName) })
                   .Where(a => a.Name != null)
                   // Only one assembly per name, to avoid passing duplicates to the compiler: the newest version, the first loaded one among equal versions.
                   .GroupBy(a => a.Name.Name)
                   .Select(grp => grp.OrderByDescending(a => a.Name.Version).First().Assembly)
                   .Select(a => CompilerReference.From(a))
                   .Concat(includeAssemblies ?? Enumerable.Empty<CompilerReference>());
        }
    }
}
