
# Roslyn compiler support

RazorEngine supports the Roslyn compiler (via the Microsoft.CodeAnalysis.CSharp nuget package).
Roslyn support is included in the `RazorEngine.NetFramework` package; no additional package is needed.

> Do not install the old `RazorEngine.Roslyn` package. It depends on the original `RazorEngine` package,
> which ships an assembly with the same name and conflicts with `RazorEngine.NetFramework`.

To activate roslyn all you need to do is set the `CompilerServiceFactory` property in the configuration:

```csharp
config.CompilerServiceFactory = new RazorEngine.Roslyn.RoslynCompilerServiceFactory();
```

## Why use Roslyn

| | CodeDom (default) | Roslyn |
|---|---|---|
| C# version in templates | C# 5 (the `csc.exe` of .NET Framework) | latest C# supported by Microsoft.CodeAnalysis.CSharp 5.6 (C# 14) |
| Compilation | starts `csc.exe` as a separate process for each template | runs in-process |

With Roslyn, templates can use modern C#, for example:

```cshtml
@($"Hello {Model.Name}!")
@(Model.Address?.City ?? "unknown")
@{ var (min, max) = (Model.Min, Model.Max); }
@{ var label = Model.Age switch { < 18 => "child", _ => "adult" }; }@label
```

Language features that need newer runtime types (for example `Span<T>`-based features, `init`/records, ranges and indices)
are not available on .NET Framework 4.8 unless those types are referenced or polyfilled.

Roslyn 5.6 depends on newer System.* packages (System.Collections.Immutable, System.Reflection.Metadata, System.Memory, …).
Your application needs binding redirects for them; SDK-style projects generate them automatically.

## Known Limitation/Bugs

- Debugging symbols do not work currently (If you know how Roslyn works please send a pull request!).
- Debug symbols cannot be created on mono/unix:

         error: (0, 0) Unexpected error writing debug information -- 'The requested feature is not implemented.'

- Only C# support (VB.NET is not supported by the Roslyn compiler service).
- If you find more please open a issue!
- Running Roslyn on mono could lead to sigsegv crashes (mono bug): https://travis-ci.org/Antaris/RazorEngine/builds/45375847!
