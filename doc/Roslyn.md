
# Roslyn compiler support

RazorEngine supports the Roslyn compiler (via the Microsoft.CodeAnalysis.CSharp nuget package).
Roslyn support is included in the `RazorEngine.NetFramework` package; no additional package is needed.

> Do not install the old `RazorEngine.Roslyn` package. It depends on the original `RazorEngine` package,
> which ships an assembly with the same name and conflicts with `RazorEngine.NetFramework`.

To activate roslyn all you need to do is set the `CompilerServiceFactory` property in the configuration:

```csharp
config.CompilerServiceFactory = new RazorEngine.Roslyn.RoslynCompilerServiceFactory();
```

## Known Limitation/Bugs

- Debugging symbols do not work currently (If you know how Roslyn works please send a pull request!).
- Debug symbols cannot be created on mono/unix:

         error: (0, 0) Unexpected error writing debug information -- 'The requested feature is not implemented.'

- Only C# support (VB.NET is not supported by the Roslyn compiler service).
- If you find more please open a issue!
- Running Roslyn on mono could lead to sigsegv crashes (mono bug): https://travis-ci.org/Antaris/RazorEngine/builds/45375847!
