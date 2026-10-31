# Reference Resolver

Templates are first transformed to a source code file and then dynamically compiled by invoking the compiler.
Because you can use source code within the template itself you are free to use any libraries within a template.
However the compiler needs to be able to resolve everything and the default strategy is to reference all currently loaded assemblies.
This can lead to problems when you want to use a library (in the template) which is not referenced in the 
hosting code or not loaded by the runtime (because it is unused).
To be able to resolve such issues you can control this behaviour and set your own `IReferenceResolver` implementation.

```csharp
config.ReferenceResolver = new MyIReferenceResolver();

class MyIReferenceResolver : IReferenceResolver {
    public string FindLoaded(IEnumerable<string> refs, string find) {
        return refs.First(r => r.EndsWith(System.IO.Path.DirectorySeparatorChar + find));
    }
    public IEnumerable<CompilerReference> GetReferences(TypeContext context, IEnumerable<CompilerReference> includeAssemblies) {
        // TypeContext gives you some context for the compilation (which templates, which namespaces and types)
        
        // You must make sure to include all libraries that are required, including mscorlib!
        // The Roslyn compiler (RazorEngine.NetFramework.Roslyn) only gets the references returned here.
        // The CodeDom compiler (the default) adds the standard libraries itself, unless mscorlib is in this list (then it uses /nostdlib).
        IEnumerable<string> loadedAssemblies = (new UseCurrentAssembliesReferenceResolver())
            .GetReferences(context, includeAssemblies)
            .Select(r => r.GetFile())
            .ToArray();

        yield return CompilerReference.From(FindLoaded(loadedAssemblies, "mscorlib.dll"));
        yield return CompilerReference.From(FindLoaded(loadedAssemblies, "System.dll"));
        yield return CompilerReference.From(FindLoaded(loadedAssemblies, "System.Core.dll"));
        yield return CompilerReference.From(typeof(MyIReferenceResolver).Assembly); // Assembly
        
        // There are several ways to load an assembly:
        //yield return CompilerReference.From("Path-to-my-custom-assembly"); // file path (string)
        //byte[] assemblyInByteArray = --- Load your assembly ---;
        //yield return CompilerReference.From(assemblyInByteArray); // byte array (roslyn only)
        //string assemblyFile = --- Get the path to the assembly ---;
        //yield return CompilerReference.From(File.OpenRead(assemblyFile)); // stream (roslyn only)
    }
}

```

By default the `UseCurrentAssembliesReferenceResolver` class is used, which will always returns all currently loaded assemblies, 
which can be resolved to a file (so this excludes dynamic assemblies).

> Always return all assemblies the templates need, including mscorlib.dll, so the resolver works with both compilers.

## Compile time with CodeDom

The CodeDom compiler (the default) starts `csc.exe` for every template, and `csc.exe` reads every reference again each time.
With the default resolver, that is every loaded assembly, so each compile gets slower the more assemblies your application has loaded.
Measured with a small template (`RunCompile` of a new template, median):

| Loaded assemblies | Default resolver | Resolver below (6 references) |
|---|---|---|
| 16 | about 120 ms | about 80 ms |
| 212 (like a larger ASP.NET application) | about 590 ms | about 80 ms |

If your templates only use a few assemblies, return just those:

```csharp
config.ReferenceResolver = new TemplateReferenceResolver();

public class TemplateReferenceResolver : IReferenceResolver
{
    public IEnumerable<CompilerReference> GetReferences(TypeContext context, IEnumerable<CompilerReference> includeAssemblies)
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,                                 // mscorlib
            typeof(Uri).Assembly,                                    // System
            typeof(Enumerable).Assembly,                             // System.Core
            typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly,  // Microsoft.CSharp, for dynamic models
            typeof(RazorEngine.Templating.ITemplate).Assembly,       // RazorEngine
            typeof(MyModel).Assembly,                                // your models and helpers
        };
        return assemblies
            .Distinct()
            .Select(CompilerReference.From)
            .Concat(includeAssemblies ?? Enumerable.Empty<CompilerReference>());
    }
}
```

Add every assembly whose types your templates use, also indirectly: for example the assembly of a model's base class,
or of a type used in a property or method signature your template calls.
A missing one fails to compile with `CS0246` (type or namespace not found) or `CS0012` (type defined in an assembly that is not referenced).

The other ways to get faster compiles without changing the references:

* The [Roslyn package](Roslyn.md): about 35 ms per compile, independent of the number of loaded assemblies, because Roslyn keeps the references in memory.
* [DotNetCompilerPlatform](DotNetCompilerPlatform.md): its compiler server keeps the references loaded between compiles.
  With about 215 references the compiler took about 120 ms instead of about 540 ms with the `csc.exe` of .NET Framework;
  with few references it is not faster.
