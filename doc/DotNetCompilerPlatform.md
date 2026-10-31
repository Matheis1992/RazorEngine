# Modern C# with DotNetCompilerPlatform

`RazorEngine.NetFramework` compiles templates with CodeDom, by default with the `csc.exe` of .NET Framework (C# 5).
With [Microsoft.CodeDom.Providers.DotNetCompilerPlatform](https://www.nuget.org/packages/Microsoft.CodeDom.Providers.DotNetCompilerPlatform)
it compiles with a newer `csc.exe` instead: C# 10 with version 4.1.0 of that package. The compiler runs in its own process,
so neither Roslyn nor its dependencies are loaded into your application, and you need no binding redirects for them.

RazorEngine has no package for this: you install the provider in your application and set it in the configuration.
The setup below is covered by the tests in `src/test/Test.RazorEngine.Core.DotNetCompilerPlatform`.

## Which compiler to choose

| | CodeDom (default) | CodeDom with DotNetCompilerPlatform 4.1.0 | Roslyn (`RazorEngine.NetFramework.Roslyn`) |
|---|---|---|---|
| C# version in templates | C# 5 | C# 10 | C# 14 |
| Where the compiler runs | `csc.exe` in its own process | newer `csc.exe` in its own process, with a compiler server (`VBCSCompiler.exe`) | in your process |
| In your process | nothing extra | one small assembly | Roslyn and its System.* dependencies, about 55 MB, with binding redirects |
| First compile in a process (benchmark) | about 0.1–0.2 s | about 2–3 s (starts the compiler server) | about 2 s |
| Each further compile (benchmark) | about 85 ms | about 100 ms | about 25 ms |
| Files to deploy | none | the `roslyn` folder (about 30 files) | the Roslyn assemblies |

Choose DotNetCompilerPlatform if your templates need C# 6 to 10, but Roslyn must not be loaded into your process, for example because
its binding redirects conflict with other libraries. Otherwise the [Roslyn package](Roslyn.md) is faster and supports newer C#.

## Installation

Install the provider in the project that is started, the web application, service or executable:

    Install-Package Microsoft.CodeDom.Providers.DotNetCompilerPlatform

Its build targets copy the compiler into the `roslyn` folder of the output folder (`bin\roslyn` in ASP.NET), where the provider looks for it.
Installing it only in a class library is not enough: the application that references the library gets the provider's assembly,
but not the `roslyn` folder, and compiling fails (see [Troubleshooting](#troubleshooting)).

Outside web projects, the package copies the `roslyn` folder only into the build output, not into the publish output:
`dotnet publish` (or publishing to a folder in Visual Studio) leaves it out. To publish it too, add this target to the project file:

```xml
<!-- Publish the compiler too: Microsoft.CodeDom.Providers.DotNetCompilerPlatform copies it only into the build output. -->
<Target Name="PublishRoslynCompiler" AfterTargets="ComputeResolvedFilesToPublishList">
  <ItemGroup>
    <_RoslynCompilerFile Include="$(OutDir)roslyn\**\*" />
    <ResolvedFileToPublish Include="@(_RoslynCompilerFile)" RelativePath="roslyn\%(RecursiveDir)%(Filename)%(Extension)" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Target>
```

Web projects publish `bin\roslyn` without it.

Version 4.x of the package needs .NET Framework 4.7.2 or newer.

## Configuration

Return a new provider from `CodeDomProviderFactory`:

```csharp
config.CompilerServiceFactory = new CodeDomCompilerServiceFactory
{
    // A new provider for every compiler service; the service disposes it.
    CodeDomProviderFactory = () => new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider()
};
```

`DefaultCompilerServiceFactory`, the default `CompilerServiceFactory`, derives from `CodeDomCompilerServiceFactory` and supports the same properties.

### Language version

Without options, templates are compiled with the newest C# version of the compiler (C# 10 with 4.1.0). To pin a version, so that a
later update of the provider does not change how your templates compile, set `CompilerOptions`:

```csharp
config.CompilerServiceFactory = new CodeDomCompilerServiceFactory
{
    CodeDomProviderFactory = () => new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider(),
    CompilerOptions = "/langversion:7.3"
};
```

Templates still run on .NET Framework: language features that need types .NET Framework does not have, such as `record` and `init`
(`System.Runtime.CompilerServices.IsExternalInit`), fail to compile.

### With IsolatedRazorEngineService

A lambda that captures variables cannot be serialized into the isolated `AppDomain`, so create the configuration there, in an `IConfigCreator`:

```csharp
[Serializable]
public class DotNetCompilerPlatformConfigCreator : IsolatedRazorEngineService.IConfigCreator
{
    public ITemplateServiceConfiguration CreateConfiguration()
    {
        return new TemplateServiceConfiguration
        {
            CompilerServiceFactory = new CodeDomCompilerServiceFactory
            {
                CodeDomProviderFactory = () => new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider()
            }
        };
    }
}

using (var service = IsolatedRazorEngineService.Create(new DotNetCompilerPlatformConfigCreator()))
{
    var result = service.RunCompile(template, "key", typeof(MyModel), model);
}
```

The provider looks for the `roslyn` folder in the application folder (`ApplicationBase`) of the isolated `AppDomain`. With the default
`AppDomain` factory, that is the same folder as your application's. If you create the `AppDomain` yourself with another `ApplicationBase`,
set `ROSLYN_COMPILER_LOCATION` (see [Troubleshooting](#troubleshooting)) or copy the `roslyn` folder there.

## Side effect: the configuration file of your project

If the project has an `App.config` or `web.config`, the package's build targets register its provider in its `<system.codedom>` section on every build.
Without such a file nothing is registered.
RazorEngine does not need this entry, because you create the provider in code. In ASP.NET, however, this entry also changes how ASP.NET compiles
its own views and pages, which is often why the package is installed in web projects anyway.

To keep your configuration file unchanged, turn the registration off in your project file:

```xml
<PropertyGroup>
  <RoslynRegisterInConfig>false</RoslynRegisterInConfig>
</PropertyGroup>
```

## The compiler server

The provider starts `VBCSCompiler.exe` from the `roslyn` folder, which keeps the compiler loaded between compiles. It is the reason the first
compile is slow and the further compiles are fast. Outside ASP.NET it ended about 10 seconds after the last compile in our tests;
the environment variable `VBCSCOMPILER_TTL` sets this time in seconds.

While it runs, it locks the files in the `roslyn` folder. When you build in Visual Studio, the package's build targets end it before they copy
new files. With `dotnet build` that does not work (you only get the warning "System.Management currently is only supported for Windows desktop
applications"), and locked files are skipped with a warning. After updating the package, end `VBCSCompiler.exe` (or wait until it ends) before you build.

## Troubleshooting

- **`Could not find a part of the path '...\roslyn\csc.exe'`**: the compiler was not copied or not deployed. Check that the `roslyn` folder
  is next to your executable (`bin\roslyn` in ASP.NET), also after publishing, and that the package is installed in the started project
  (see [Installation](#installation)). To use a compiler in another folder, set the environment variable `ROSLYN_COMPILER_LOCATION`
  or the app setting `aspnet:RoslynCompilerLocation` to the folder that contains `csc.exe`.
- **C# 6 or newer syntax still fails**: the configuration does not use the provider. `CodeDomProviderFactory` must be set on the factory
  that the service actually uses; with `IsolatedRazorEngineService`, in the `IConfigCreator`.
- **`TemplateCompilationException` mentioning a language version**: the template uses a feature that is newer than `/langversion`
  in `CompilerOptions`, or newer than the provider's compiler.

For more CodeDom options (`CompilerOptions`, your own `CompilerParameters`), see [Roslyn: Modern C# with CodeDom](Roslyn.md#modern-c-with-codedom).
