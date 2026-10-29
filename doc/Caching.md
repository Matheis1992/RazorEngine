# Caching of compiled templates

Compiling a template is the expensive part: Razor turns it into C#, Roslyn compiles it into an assembly and the assembly is loaded.
Running an already compiled template is cheap. So `RazorEngineService` caches compiled templates, and you choose how with an `ICachingProvider`.

## How the cache works

A compiled template is cached under two things:

* **The template key**, from the `ITemplateManager` (for example the name you pass to `RunCompile`, or the full path with `ResolvePathTemplateManager`).
* **The model type.** The same template compiled for `typeof(Person)` and for `typeof(Order)` are two cache entries.
  `null` and dynamic models (`dynamic`, `ExpandoObject`, `DynamicViewBag`, ...) share one entry.
  A template with an `@model` directive is also cached under that model type.

The service methods use the cache like this:

| Method | Cached | Not cached |
|---|---|---|
| `RunCompile(name, modelType, model)` | runs the cached template | compiles, caches, runs |
| `Compile(name, modelType)` | compiles again and replaces the entry | compiles and caches |
| `Run(name, modelType, model)` | runs the cached template | throws `InvalidOperationException` ("No template exists with key ...") |
| `IsTemplateCached(name, modelType)` | `true` | `false` |

Use the same model type on every call for a template (or always `null`), otherwise each type is compiled separately.

```csharp
var config = new TemplateServiceConfiguration
{
    TemplateManager = new ResolvePathTemplateManager(new[] { @"C:\MyApp\Templates" })
};
using (var service = RazorEngineService.Create(config))
{
    // Compile ahead of time, for example at startup ...
    service.Compile("Invoice", typeof(Invoice));

    // ... then only run it: no compile here.
    string html = service.Run("Invoice", typeof(Invoice), invoice);
}
```

## Caching providers

| Provider | Behaviour |
|---|---|
| `DefaultCachingProvider` (default) | Caches every compiled template for the lifetime of the service. Templates are never compiled twice, but a changed template is not noticed either. |
| `InvalidatingCachingProvider` | Like the default, plus `InvalidateCache(key)`, `InvalidateCacheOfType(key, modelType)` and `InvalidateAll()`. The next `RunCompile` compiles again. |
| `InvalidatingByModifiedTimeCachingProvider` | Compiles again when the template file changed (its modification time). Only works together with `ResolvePathCheckModifiedTimeTemplateManager`, which puts the modification time into the key. |

Invalidate a template yourself, for example after a user edited it:

```csharp
var cache = new InvalidatingCachingProvider();
var config = new TemplateServiceConfiguration
{
    TemplateManager = new ResolvePathTemplateManager(new[] { @"C:\MyApp\Templates" }),
    CachingProvider = cache
};
using (var service = RazorEngineService.Create(config))
{
    service.RunCompile("Invoice", typeof(Invoice), invoice);  // compiles
    // ... the template file changes ...
    cache.InvalidateCache(service.GetKey("Invoice"));
    service.RunCompile("Invoice", typeof(Invoice), invoice);  // compiles the new version
}
```

Or let the modification time decide:

```csharp
var config = new TemplateServiceConfiguration
{
    TemplateManager = new ResolvePathCheckModifiedTimeTemplateManager(new[] { @"C:\MyApp\Templates" }),
    CachingProvider = new InvalidatingByModifiedTimeCachingProvider()
};
```

`WatchingResolvePathTemplateManager` does the same with a `FileSystemWatcher`: it invalidates an `InvalidatingCachingProvider` when a file in the template folders changes.
See [TemplateManager](TemplateManager.md) for the template managers and for writing your own `ICachingProvider`.

## Memory: invalidating does not free anything

Each compile loads a new assembly, and a .NET Framework assembly can only be unloaded together with its `AppDomain`.
Invalidating a template removes it from the cache, but its old assembly stays in memory.
So a process that recompiles changed templates again and again grows with every recompile.

Measured with a small template (600 recompiles, each with a new version of the template):

| | After 100 recompiles | After 600 recompiles |
|---|---|---|
| `RazorEngineService` in your `AppDomain` | +12 MB | +35 MB, and still growing by about 45 KB per recompile |
| `IsolatedRazorEngineService`, recreated every 100 recompiles | +24 MB | +29 MB, stays flat |

The isolated variant starts higher, because every new `AppDomain` loads its own Roslyn, but it does not grow.

That is fine when templates change rarely: 1,000 recompiles cost about 45 MB. If they change often:

* Run the templates in an `IsolatedRazorEngineService` and create a new one from time to time.
  Disposing it unloads its `AppDomain` with all template assemblies (see [Isolation](Isolation.md)).
  Models then cross the `AppDomain` boundary, so they must be `[Serializable]` (or be wrapped, see Isolation).
* Or recycle the application (for example the IIS application pool) regularly.

## Temporary files

Every compile writes its files (source, assembly, debug symbols) to a new `RazorEngine_*` folder in the temp directory.
The caching provider registers these folders for deletion, which happens when the `AppDomain` is unloaded,
because the loaded assemblies lock their files until then. In the default `AppDomain` this is not possible;
see "Temporary files" in the [README](https://github.com/Matheis1992/RazorEngine/blob/master/README.md#temporary-files).

## Not to be confused with the reference cache

Since 4.2.0, the Roslyn compiler also caches the loaded assemblies templates are compiled *against*
(the .NET Framework and your application's assemblies), so it does not read them again for every template.
References a custom `IReferenceResolver` gives as a file path are read on every compile.
That cache is internal, needs no configuration and does not cache templates: a changed template is always compiled again
when its entry is invalidated or missing.
