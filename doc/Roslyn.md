
# Roslyn compiler support

Since 5.0, Roslyn is in its own package. `RazorEngine.NetFramework` compiles templates with CodeDom (C# 5) by default
and depends only on Microsoft.AspNet.Razor. To compile templates in-process with Roslyn (modern C#), install the Roslyn package:

    Install-Package RazorEngine.NetFramework.Roslyn

and set its factory:

```csharp
config.CompilerServiceFactory = new RazorEngine.Roslyn.RoslynCompilerServiceFactory();
```

or in `app.config`/`web.config`:

```xml
<razorEngine compilerServiceFactoryType="RazorEngine.Roslyn.RoslynCompilerServiceFactory, RazorEngine.NetFramework.Roslyn" />
```

Installing the package alone does not change how templates are compiled; you choose Roslyn explicitly.

> Do not install the old `RazorEngine.Roslyn` package. It depends on the original `RazorEngine` package,
> which ships an assembly with the same name and conflicts with `RazorEngine.NetFramework`.

Without the Roslyn package, CodeDom can still compile modern C# with another `CodeDomProvider`, see [Modern C# with CodeDom](#modern-c-with-codedom)
and the step-by-step guide [DotNetCompilerPlatform](DotNetCompilerPlatform.md).

Templates are always C#: VB.NET templates (`.vbhtml`, `Language.VisualBasic`) are not supported since version 4.

## Roslyn compared to CodeDom

| | CodeDom (default) | Roslyn (`RazorEngine.NetFramework.Roslyn`) |
|---|---|---|
| C# version in templates | C# 5 (the `csc.exe` of .NET Framework) | latest C# supported by Microsoft.CodeAnalysis.CSharp 5.6 (C# 14) |
| Compilation | starts `csc.exe` as a separate process for each template | runs in-process; the first compilation in a process (or `AppDomain`) is slower because Roslyn has to be loaded |
| First compile in a process (benchmark) | about 0.2 s | about 2 s |
| Each further compile (benchmark) | about 85 ms, more with many loaded assemblies (see [Reference resolver](ReferenceResolver.md#compile-time-with-codedom)) | about 25 ms |
| Memory | the compiler runs in its own process | Roslyn stays loaded in your process: about 55 MB more |

From about 30 templates per process on, Roslyn is faster overall.

### Warming up Roslyn

The first compile in a process takes about 2 s because Roslyn itself is loaded and compiled to machine code then, independent of your template.
To keep that wait away from the first user, compile a small template at startup in the background:

```csharp
Task.Run(() =>
{
    var config = new TemplateServiceConfiguration { CompilerServiceFactory = new RoslynCompilerServiceFactory() };
    using (var service = RazorEngineService.Create(config))
    {
        service.RunCompile("@Model.Length @foreach (var c in Model) {<i>@c.ToString().ToUpper()</i>} @if (Model.Any()) {<b>x</b>}",
            "warmup", typeof(string), "x");
    }
});
```

This works for every later service in the same `AppDomain`. Use a warm-up template with the constructs your templates use (loops, conditions, LINQ):
with the template above, the first real compile took about 50 ms; with just `@Model` it still took about 250 ms.

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
You can pin the language version (`LanguageVersion` is in `Microsoft.CodeAnalysis.CSharp`):

```csharp
config.CompilerServiceFactory = new RoslynCompilerServiceFactory { LanguageVersion = LanguageVersion.CSharp13 };
```

A pinned version also rejects newer syntax, so pick the newest version whose behaviour your templates rely on.

If you configure RazorEngine in `app.config`/`web.config`, the factory is created from its type name, so derive a small factory
that sets the version and name that type:

```csharp
public class CSharp13CompilerServiceFactory : RoslynCompilerServiceFactory
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

The Roslyn package depends on Roslyn 5.6, which depends on newer System.* packages (System.Collections.Immutable, System.Reflection.Metadata, System.Memory, …).
Your application needs binding redirects for them; SDK-style projects generate them automatically.
If you create your own `AppDomain` for `IsolatedRazorEngineService`, give it your configuration file
(`AppDomainSetup.ConfigurationFile`), otherwise the binding redirects do not apply there (see [Isolation](Isolation.md)).
Without the Roslyn package, none of this is needed.

## Modern C# with CodeDom

CodeDom uses the `csc.exe` of .NET Framework (C# 5) by default. `CodeDomCompilerServiceFactory` (and `DefaultCompilerServiceFactory`,
which derives from it) can compile with another `CodeDomProvider` instead, for example
[Microsoft.CodeDom.Providers.DotNetCompilerPlatform](https://www.nuget.org/packages/Microsoft.CodeDom.Providers.DotNetCompilerPlatform),
which runs a newer `csc.exe` in a separate process, so Roslyn is not loaded into your process:

```csharp
config.CompilerServiceFactory = new CodeDomCompilerServiceFactory
{
    // A new provider for every compiler service; the service disposes it.
    CodeDomProviderFactory = () => new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider(),
    // Optional compiler switches, appended to RazorEngine's own.
    CompilerOptions = "/langversion:7.3"
};
```

[DotNetCompilerPlatform](DotNetCompilerPlatform.md) describes this provider step by step: installation, language version, deployment and troubleshooting.

`CodeDomProviderFactory` must return a new provider on every call (never `null`). With `IsolatedRazorEngineService`, create the factory
in your `IConfigCreator.CreateConfiguration` (inside the new `AppDomain`): a lambda that captures variables cannot be serialized into it.

`CodeDomProvider.CreateProvider("cs")` returns the C# provider configured in the `<system.codedom>` section of your configuration file.
It does not apply the `compilerOptions` of that section, so set them in `CompilerOptions`.
To adjust the `CompilerParameters` further, derive from `CSharpDirectCompilerService`, override `ConfigureCompilerParameters`,
and create your service in a factory derived from `CodeDomCompilerServiceFactory`:

```csharp
public class MyCompilerService : CSharpDirectCompilerService
{
    public MyCompilerService(CodeDomProvider provider) : base(provider) { }

    protected override void ConfigureCompilerParameters(CompilerParameters parameters)
    {
        parameters.WarningLevel = 0;
    }
}

public class MyCompilerServiceFactory : CodeDomCompilerServiceFactory
{
    protected override DirectCompilerServiceBase CreateCSharpCompilerService(CodeDomProvider codeDomProvider)
    {
        // codeDomProvider is null unless CodeDomProviderFactory is set.
        return new MyCompilerService(codeDomProvider ?? new Microsoft.CSharp.CSharpCodeProvider());
    }
}
```

`CodeDomProviderFactory` and `CompilerOptions` keep working with your factory.

## Known limitations

- With Roslyn, `Debug = true` does not let you step into templates with the debugger.
  If you need that, use CodeDom (the default).
- If you find more, please open an issue: https://github.com/Matheis1992/RazorEngine/issues
