# RazorEngine

[![CI](https://github.com/Matheis1992/RazorEngine/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/Matheis1992/RazorEngine/actions/workflows/ci.yml)

This is a maintained fork of [Antaris/RazorEngine](https://github.com/Antaris/RazorEngine), which has not been updated since 2018.
It targets .NET Framework 4.8 and is published as the NuGet package `RazorEngine.NetFramework`.
The original package `RazorEngine` (3.10.x) stays on nuget.org but is no longer maintained.

The assembly is `RazorEngine.NetFramework.dll` with its own signing key, so it never overwrites or replaces the original `RazorEngine.dll`.
The namespaces are unchanged (`RazorEngine`, `RazorEngine.Templating`, ...), so switching from the original package usually only means replacing the package
and updating assembly names in `app.config`/`web.config` (see the [release notes](https://github.com/Matheis1992/RazorEngine/blob/master/doc/ReleaseNotes.md)).

If another library still pulls in the original package, both can be loaded in the same application, with these limits:

 * Templates compiled by this fork ignore the original assembly, so they keep working.
 * A project that references both packages directly sees the same type names twice and needs an [`extern alias`](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/extern-alias).
 * Both read the `razorEngine` configuration section, so only the one whose type is registered there gets configured; the other uses its defaults.

The documentation of the original project is available on https://antaris.github.io/RazorEngine/.


## Quickstart

First install the nuget package.

	Install-Package RazorEngine.NetFramework

A templating engine built on Microsoft's Razor parsing engine, RazorEngine allows you to use Razor syntax to build dynamic templates.
You can find an introduction [here](http://www.asp.net/web-pages/overview/getting-started/introducing-razor-syntax-%28c%29).
All you need to do is use the static `Engine` class (the `Engine.Razor` instance) in the 'RazorEngine' namespace:

```csharp
using RazorEngine;
using RazorEngine.Templating; // For extension methods.

string template = "Hello @Model.Name, welcome to RazorEngine!";
var result =
	Engine.Razor.RunCompile(template, "templateKey", null, new { Name = "World" });
```

> The `RunCompile` method used here is an extension method and you need to open the `RazorEngine.Templating` namespace.

The `"templateKey"` must be unique and after running the above example you can re-run the cached template with this key.

```csharp
// using RazorEngine.Templating; // Dont forget to include this.
var result =
	Engine.Razor.Run("templateKey", null, new { Name = "Max" });
```

The null parameter is the `modelType` and `null` in this case means we use `dynamic` as the type of the model.
You can use a static model as well by providing a type object.

```csharp
// using RazorEngine.Templating; // Dont forget to include this.
var result =
	Engine.Razor.RunCompile("templateKey", typeof(Person), new Person { Name = "Max" });
```

Note that we now re-compile the model with a different type. 
When you do not run the same template a lot of times (like several 1000 times), compiling uses the most time.
So the benefit you get from a static type will most likely not compensate the additional compile time.
Therefore you should either stick to one type for a template (best of both worlds) or just use (the slower) `dynamic` (`null`).
You can specify the `modelType` of a template with the `@model` directive. 
When you do this the `modelType` parameter is ignored, but you should use the same type instance (or `null`) 
on every call to prevent unnecessary re-compilations because of type mismatches in the caching layer.

## Configuration

You can configure RazorEngine with the `TemplateServiceConfiguration` class.

```csharp
var config = new TemplateServiceConfiguration();
// .. configure your instance

var service = RazorEngineService.Create(config);
```

If you want to use the static `Engine` class with this new configuration:

```csharp
Engine.Razor = service;
```


### General Configuration

By default RazorEngine is configured to encode using Html. 
This supports the majority of users but with some configuration changes you can also set it to encode using Raw format 
which is better suited for templates that generate things like javascript, php, C# and others.

```csharp
config.EncodedStringFactory = new RawStringFactory(); // Raw string encoding.
config.EncodedStringFactory = new HtmlEncodedStringFactory(); // Html encoding.
```

### Compiler

Templates are compiled in-process with Roslyn, so they can use modern C# (up to C# 14),
for example string interpolation, `?.`, tuples and switch expressions.
On .NET Framework, Roslyn's dependencies need binding redirects; SDK-style projects generate them automatically.
See [Roslyn](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Roslyn.md) for details.

To use the old CodeDom compiler (`csc.exe`, C# 5) instead, for example to step into templates with the debugger:

```csharp
// using RazorEngine.Compilation;
config.CompilerServiceFactory = new CodeDomCompilerServiceFactory();
```

### Debugging

One thing you might want to enable is the debugging feature:

```csharp
config.Debug = true;
config.CompilerServiceFactory = new CodeDomCompilerServiceFactory(); // Roslyn templates cannot be stepped into yet.
```

When `Debug` is true you can straight up debug into the generated code. 
RazorEngine also supports debugging directly into the template files (normally `.cshtml` files).
As you might see in the above code there is no file to debug into.
To provide RazorEngine with the necessary information you need to tell where the file can be found:

```csharp
// using RazorEngine.Templating; // Dont forget to include this.
string template = "Hello @Model.Name, welcome to RazorEngine!";
string templateFile = "C:/mytemplate.cshtml"
var result =
	Engine.Razor.RunCompile(new LoadedTemplateSource(template, templateFile), "templateKey", null, new { Name = "World" });
```

This time when debugging the template you will jump right into the template file.

### Set a template manager

The API is designed around the idea that you do not have the templates sitting around in the source code 
(while you can do that as seen above).
The main interface to provide RazorEngine with templates is the `ITemplateManager` interface.	
You should either pick one of the available implementations or write your own.
See [TemplateManager and Caching documentation](https://github.com/Matheis1992/RazorEngine/blob/master/doc/TemplateManager.md) for details.

## Temporary files

RazorEngine tries hard to delete the temporary files it creates, but this is not always possible.
This is especially true if you run RazorEngine from the default `AppDomain`.
RazorEngine will warn you in this situation by writing to the stderr. 
One way to switch into a new AppDomain is to use the following snippet:

```csharp
static int Main(string[] args)
{
    if (AppDomain.CurrentDomain.IsDefaultAppDomain())
    {
        // RazorEngine cannot clean up from the default appdomain...
        Console.WriteLine("Switching to second AppDomain, for RazorEngine...");
        // A fully trusted AppDomain with the same settings, including the configuration file (binding redirects).
        // Do not use a partially trusted (sandboxed) AppDomain: it is no security boundary for templates (CVE-2021-46703).
        var domain = AppDomain.CreateDomain("MyMainDomain", null, AppDomain.CurrentDomain.SetupInformation);
        var exitCode = domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location);
        // RazorEngine will cleanup. 
        AppDomain.Unload(domain);
        return exitCode;
    }
    // Continue with your code.
}
```

Depending on your scenario you probably need to edit it to your needs.
Note that you need to `Unload` the domain to trigger cleanup.

For the following scenario:

 * Your templates are limited in number.
 * You fully trust your templates / don't need isolation.
 * You don't need any kind of debugging support.
 * Your templates do not change in runtime.

You can use `config.DisableTempFileLocking = true` as well. This will work in any AppDomain (including the default one).
To remove the RazorEngine warnings you can additionally use `config.CachingProvider = new DefaultCachingProvider(t => {})`.

See also https://github.com/Antaris/RazorEngine/issues/244 for more details.


## Security

Templates are C# code and run with the full rights of your application.
Only use templates from trusted sources, and never build templates by concatenating user input (pass it as model data instead).

`IsolatedRazorEngineService` is meant for unloading templates, not as a sandbox. Partially trusted (sandboxed) `AppDomain`s
are refused, because templates can escape them ([CVE-2021-46703](https://github.com/advisories/GHSA-ph3v-2hq5-5qfq)).
See [Isolation](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Isolation.md) for details.

## More

The documentation is in the [doc](https://github.com/Matheis1992/RazorEngine/tree/master/doc) folder. Start with [About Razor](https://github.com/Matheis1992/RazorEngine/blob/master/doc/AboutRazor.md) and [Template basics](https://github.com/Matheis1992/RazorEngine/blob/master/doc/TemplateBasics.md).

 * [About Razor](https://github.com/Matheis1992/RazorEngine/blob/master/doc/AboutRazor.md): the Razor syntax.
 * [Template basics](https://github.com/Matheis1992/RazorEngine/blob/master/doc/TemplateBasics.md): typed, anonymous and dynamic models, the supported syntax and how to extend it.
 * [Layout and partial templates](https://github.com/Matheis1992/RazorEngine/blob/master/doc/LayoutAndPartial.md)
 * [Encoding](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Encoding.md): HTML and raw encoding of values.
 * [Template manager and caching](https://github.com/Matheis1992/RazorEngine/blob/master/doc/TemplateManager.md): where templates come from and how compiled templates are cached.
 * [Reference resolver](https://github.com/Matheis1992/RazorEngine/blob/master/doc/ReferenceResolver.md): which assemblies templates can use.
 * [Roslyn](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Roslyn.md): the compiler, the C# language version and binding redirects.
 * [Isolation](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Isolation.md): unloading templates with `IsolatedRazorEngineService`.
 * [Intellisense and ReSharper](https://github.com/Matheis1992/RazorEngine/blob/master/doc/IntellisenseAndResharper.md): editor support for template files.
 * [Upgrading](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Upgrading.md) and [Release notes](https://github.com/Matheis1992/RazorEngine/blob/master/doc/ReleaseNotes.md)

