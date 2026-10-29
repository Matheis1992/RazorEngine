
# Roslyn compiler support

Since version 4, C# templates are compiled with Roslyn (via the Microsoft.CodeAnalysis.CSharp nuget package) by default.
Roslyn support is included in the `RazorEngine.NetFramework` package; no additional package and no configuration is needed.

> Do not install the old `RazorEngine.Roslyn` package. It depends on the original `RazorEngine` package,
> which ships an assembly with the same name and conflicts with `RazorEngine.NetFramework`.

`DefaultCompilerServiceFactory` (the default `CompilerServiceFactory`) compiles templates with Roslyn.
To compile templates with CodeDom (C# 5) as before version 4, set:

```csharp
config.CompilerServiceFactory = new RazorEngine.Compilation.CodeDomCompilerServiceFactory();
```

`RazorEngine.Roslyn.RoslynCompilerServiceFactory` is still available and does the same as `DefaultCompilerServiceFactory`.

Templates are always C#: VB.NET templates (`.vbhtml`, `Language.VisualBasic`) are not supported since version 4.

## Roslyn compared to CodeDom

| | CodeDom | Roslyn (default) |
|---|---|---|
| C# version in templates | C# 5 (the `csc.exe` of .NET Framework) | latest C# supported by Microsoft.CodeAnalysis.CSharp 5.6 (C# 14) |
| Compilation | starts `csc.exe` as a separate process for each template | runs in-process; the first compilation in a process (or `AppDomain`) is slower because Roslyn has to be loaded |
| First compile in a process (benchmark) | about 0.2 s | about 2 s |
| Each further compile (benchmark) | about 85 ms | about 25 ms |
| Memory | the compiler runs in its own process | Roslyn stays loaded in your process: about 55 MB more |

From about 30 templates per process on, Roslyn is faster overall.

With Roslyn, templates can use modern C#, for example:

```cshtml
@($"Hello {Model.Name}!")
@(Model.Address?.City ?? "unknown")
@{ var (min, max) = (Model.Min, Model.Max); }
@{ var label = Model.Age switch { < 18 => "child", _ => "adult" }; }@label
```

Language features that need newer runtime types (for example `Span<T>`-based features, `init`/records, ranges and indices)
are not available on .NET Framework 4.8 unless those types are referenced or polyfilled.

## C# language version

Templates are compiled with the newest C# version the bundled Roslyn supports (C# 14).
A newer C# version can change the meaning of existing code without a compile error. For example, since C# 14,
`field` inside a property accessor is the property's backing field, not a member named `field`.
Since 4.2 you can pin the language version (`LanguageVersion` is in `Microsoft.CodeAnalysis.CSharp`):

```csharp
config.CompilerServiceFactory = new DefaultCompilerServiceFactory { LanguageVersion = LanguageVersion.CSharp13 };
```

A pinned version also rejects newer syntax, so pick the newest version whose behaviour your templates rely on.

If you configure RazorEngine in `app.config`/`web.config`, the factory is created from its type name, so derive a small factory
that sets the version and name that type:

```csharp
public class CSharp13CompilerServiceFactory : DefaultCompilerServiceFactory
{
    public CSharp13CompilerServiceFactory()
    {
        LanguageVersion = LanguageVersion.CSharp13;
    }
}
```

```xml
<razorEngine compilerServiceFactoryType="MyApp.CSharp13CompilerServiceFactory, MyApp" />
```

## Binding redirects

Roslyn 5.6 depends on newer System.* packages (System.Collections.Immutable, System.Reflection.Metadata, System.Memory, …).
Your application needs binding redirects for them; SDK-style projects generate them automatically.
If you create your own `AppDomain` for `IsolatedRazorEngineService`, give it your configuration file
(`AppDomainSetup.ConfigurationFile`), otherwise the binding redirects do not apply there (see [Isolation](Isolation.md)).

## Known limitations

- With Roslyn, `Debug = true` does not let you step into templates with the debugger.
  If you need that, use `CodeDomCompilerServiceFactory`.
- If you find more, please open an issue: https://github.com/Matheis1992/RazorEngine/issues
