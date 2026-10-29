### 4.2.0

 * (NEW) The C# language version of templates can be pinned with `DefaultCompilerServiceFactory.LanguageVersion` (also on `RoslynCompilerServiceFactory`, and `CSharpRoslynCompilerService.LanguageVersion`). The default stays the newest version the bundled Roslyn supports (C# 14). Pin an older version when a newer C# version changes the meaning of existing templates, see the `field` entry under 4.1.0:

   ```csharp
   config.CompilerServiceFactory = new DefaultCompilerServiceFactory { LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp13 };
   ```

 * (PERFORMANCE) The Roslyn compiler service caches the metadata references of the referenced assemblies per `AppDomain`, so Roslyn reuses what it read from them instead of reading and analyzing every referenced assembly on each compile. Each compile after the first is about 30% faster: in a benchmark, about 27 ms instead of 40 ms with 64 loaded assemblies, and about 65 ms instead of 95 ms with 255. The first compile in a process is unchanged (about 2 s for loading Roslyn). A referenced file that changes on disk (modification time or size) is read again. Templates themselves are not cached by this; changed templates are recompiled as before.
 * (BUILD) `dotnet pack` validates the package against the last published version (package validation, baseline 4.1.0) and fails on breaking changes of the public API.

### 4.1.0

4.1.0 switches templates to a modern C# compiler and removes the APIs that were obsolete since 3.5, as well as VB.NET templates.
Unchanged: the template syntax (Razor 3, Microsoft.AspNet.Razor 3.3.0), the namespaces, the assembly name `RazorEngine.NetFramework.dll` and its signing key, and .NET Framework 4.8.
Read the breaking changes below before updating; [doc/Upgrading.md](https://github.com/Matheis1992/RazorEngine/blob/master/doc/Upgrading.md) has the migration steps.

#### Highlights

 * (NEW) **Modern C# in templates.** Roslyn (Microsoft.CodeAnalysis.CSharp) is updated from 1.3.2 to 5.6.0 and is now the default compiler. Templates can use C# up to C# 14 instead of C# 5: string interpolation, `?.`, `nameof`, tuples and deconstruction, pattern matching, switch expressions and more. For example:

   ```cshtml
   <p>@($"Hello {Model.Name}!")</p>
   <p>@(Model.Nickname?.ToUpper() ?? "no nickname")</p>
   @{ var text = Model.Age switch { >= 18 => "adult", _ => "child" }; }
   <p>@text</p>
   ```

 * (NEW) **Fewer dependencies.** The package now only depends on Microsoft.AspNet.Razor 3.3.0 and Microsoft.CodeAnalysis.CSharp 5.6.0. The pins of System.Collections.Immutable 1.3.1, System.Reflection.Metadata 1.4.2 and System.Security.Cryptography.X509Certificates 4.3.2, which only existed for the old Roslyn, are gone.

#### Breaking changes

 * (BREAKING) **Roslyn is the default compiler.** `DefaultCompilerServiceFactory` now compiles templates in-process with Roslyn instead of starting `csc.exe` through CodeDom.
   - Templates are still compiled optimized and with the `RAZORENGINE` preprocessor symbol, as with CodeDom, so `#if RAZORENGINE` keeps working.
   - The first compile in a process (or `AppDomain`) takes longer, because Roslyn is loaded and JIT-compiled there once: about 2 s instead of about 0.2 s with CodeDom. Later compiles do not start a separate `csc.exe` process and are faster (in a benchmark about 40 ms instead of about 85 ms; 4.2.0 brings this down to about 26 ms). From about 30 templates per process on, Roslyn is faster overall.
   - Roslyn runs inside the process: it needs about 55 MB more memory per process, and per `AppDomain` of `IsolatedRazorEngineService`.
   - **Debugging:** with Roslyn, `Debug = true` does not let you step into templates with the debugger.
   - To keep the old compiler (C# 5, `csc.exe`), for example for debugging, use the new `CodeDomCompilerServiceFactory`:

     ```csharp
     config.CompilerServiceFactory = new RazorEngine.Compilation.CodeDomCompilerServiceFactory();
     ```

     or in the configuration file: `<razorEngine compilerServiceFactoryType="RazorEngine.Compilation.CodeDomCompilerServiceFactory, RazorEngine.NetFramework">`.
   - `RoslynCompilerServiceFactory` still exists and now does the same as `DefaultCompilerServiceFactory`.

 * (BREAKING) **C# 14 can change the meaning of existing template code.** Templates were compiled as C# 5 before and are compiled with the newest C# version now. Code that compiles with both usually behaves the same, with one known exception that does not cause a compile error:
   - Since C# 14, `field` inside a property accessor is the property's backing field. A template that declares its own member named `field` (for example in `@functions`) and returns it from a property accessor now gets the backing field instead (for example `null`).
   - Rename the member, or, from 4.2.0 on, pin `LanguageVersion.CSharp13` (see 4.2.0), or keep the CodeDom compiler.
   - Roslyn lists all C# breaking changes in https://github.com/dotnet/roslyn/tree/main/docs/compilers/CSharp.

 * (BREAKING) **Binding redirects for Roslyn's dependencies.** Roslyn 5.6.0 brings newer System.* assemblies (System.Collections.Immutable 10, System.Reflection.Metadata 10, System.Memory and others). .NET Framework applications need binding redirects for them:
   - SDK-style projects generate them automatically (`AutoGenerateBindingRedirects`).
   - For packages.config projects, NuGet adds them to `app.config`/`web.config` on install. Check that existing, hand-written redirects for these assemblies point to the new versions.
   - A `FileLoadException` for one of these assemblies at the first template compile means a binding redirect is missing.
   - Custom `AppDomain`s for `IsolatedRazorEngineService` need the application's configuration file, otherwise the redirects do not apply there. `IsolatedRazorEngineService.Create()` already does this; for your own domains set it yourself:

     ```csharp
     var setup = new AppDomainSetup
     {
         ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
         ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
     };
     var service = IsolatedRazorEngineService.Create(() => AppDomain.CreateDomain("Templates", null, setup));
     ```

 * (BREAKING) **Legacy API removed** (obsolete since 3.5). Replacements:

   | Removed | Use instead |
   |---|---|
   | `Razor.Parse(template, model, name)` | `Engine.Razor.RunCompile(template, name, null, model)` |
   | `Razor.Compile(template, modelType, name)` | `Engine.Razor.Compile(template, name, modelType)` |
   | `Razor.Run(name, model)` | `Engine.Razor.Run(name, null, model)` |
   | `TemplateService`, `ITemplateService` | `RazorEngineService.Create(config)`, `IRazorEngineService` |
   | `IsolatedTemplateService` | `IsolatedRazorEngineService.Create(...)` |
   | `ITemplate.TemplateService`, `TemplateBase.TemplateService` | `TemplateBase.Razor` |
   | Parallel query plans (`IParallelQueryPlan`, ...) | your own `Parallel.ForEach`/PLINQ around `Run` |

   The `RunCompile`/`Compile`/`Run` overloads with a template string are extension methods in `RazorEngine.Templating`.

 * (BREAKING) **Template resolvers removed** (obsolete since 3.5). Template managers replace them:

   | Removed | Use instead |
   |---|---|
   | `ITemplateResolver`, `DelegateTemplateResolver` | `ITemplateManager`, e.g. `DelegateTemplateManager`, `ResolvePathTemplateManager`, `EmbeddedResourceTemplateManager` |
   | `config.Resolver` | `config.TemplateManager` |
   | `ResolveUsing(ITemplateResolver)`, `ResolveUsing<TResolver>()` | `ManageUsing(...)`, `ManageUsing<TManager>()` (`ResolveUsing(Func<string, string>)` stays) |
   | `templateResolverType="..."` in the configuration file | `templateManagerType="..."` |
   | `WrapperTemplateManager` | an `ITemplateManager` implementation |

   A configuration file that still sets `templateResolverType` now fails to load with a `ConfigurationErrorsException` instead of being ignored.

 * (BREAKING) **Code inspectors removed** (obsolete since 3.5): `ICodeInspector`, the `CodeInspectors` properties of the compiler service and the configurations, `AddInspector` and `Inspect()`. There is no replacement. They were no security boundary either: templates must come from trusted sources.

 * (BREAKING) **APIs announced for removal in 4.0 removed:**
   - `TemplateCompilationException.Errors`: use `CompilerErrors`.
   - `ICompilerService.IncludeAssemblies()` and `CompilerServiceBase.IncludeAssemblies()`: use `IncludeReferences()`.
   - The `ExecuteContext(DynamicViewBag)` constructor: it always threw; pass the view bag to `Run`/`RunCompile` instead.
   - The public `TypeHash` constructors of the embedded Impromptu code: use `TypeHash.Create`.

 * (BREAKING) **VB.NET templates removed.** Removed are `Language.VisualBasic`, the `RazorEngine.Compilation.VisualBasic` compiler services, `CompilerServicesUtility.ResolveVBTypeName`/`VBGetRawTypeName`/`VBCreateGenericType`, the VB parser in `RazorEngineHost` and the `.vbhtml` lookup of `ResolvePathTemplateManager`.
   - Templates are always C#. Convert `.vbhtml` templates to `.cshtml`.
   - A configuration file with `language="VisualBasic"` or `defaultLanguage="VisualBasic"` fails to load.
   - VB.NET applications can still use RazorEngine with C# templates and pass their own (also anonymous) objects as models.

#### Fixes

 * (BUGFIX) The Roslyn compiler service now prepares the generated code like the CodeDom one. Before, with Roslyn, base template types with constructor parameters failed to compile, dynamic models were not marked, and `#line hidden` was not removed for debugging.
 * (BUGFIX) The Roslyn compiler service now compiles optimized and defines `RAZORENGINE`, like CodeDom always did.

#### Build and tests

 * (BUILD) New tests check that both compilers optimize and define `RAZORENGINE`, compile modern C# (C# 6 to 8 samples) with Roslyn, and keep the CodeDom fallback tested.
 * (BUILD) The release workflow publishes tagged versions to nuget.org with trusted publishing (introduced for 4.0.0).
 * (PACKAGING) The copyright notice of the package and the assembly keeps the original project's notice and adds the fork's: `Copyright © 2011-2017 RazorEngine Project, © 2026 RazorEngine.NetFramework contributors`.

### 4.0.0 (RazorEngine.NetFramework)

 * (BREAKING) First release of the fork, published as the new package `RazorEngine.NetFramework`.
 * (SECURITY, BREAKING) Fix CVE-2021-46703 (sandbox escape in `IsolatedRazorEngineService`): `IsolatedRazorEngineService` and `IsolatedTemplateService` now refuse partially trusted (sandboxed) `AppDomain`s with a `NotSupportedException`, because Code Access Security is no security boundary. Isolation with fully trusted `AppDomain`s (to unload templates) works as before. Templates must come from trusted sources; see doc/Isolation.md.
 * (BREAKING) The assembly is now `RazorEngine.NetFramework.dll`, signed with a new key (public key token `58ad996d4f48e61f`), so it no longer collides with the original `RazorEngine.dll`. Namespaces stay `RazorEngine.*`. When migrating, update `app.config`/`web.config`:
   - Change the assembly name of the section registration, e.g. `type="RazorEngine.Configuration.RazorEngineConfigurationSection, RazorEngine.NetFramework"`.
   - Change every assembly-qualified type name inside `<razorEngine>` that names `, RazorEngine` (`activatorType`, `baseTemplateType`, `cachingProviderType`, `compilerServiceFactoryType`, `encodedStringFactoryType`, `referenceResolverType`, `templateManagerType`, `templateResolverType`).
   - Remove `bindingRedirect` entries for the old `RazorEngine` assembly.
 * (BUGFIX) The default reference resolver no longer passes the original `RazorEngine`/`RazorEngine.Roslyn` 3.x assemblies to the template compiler, so templates still compile when the original package is loaded in the same process.
 * (NEW) Roslyn is part of the package. In the original project it was the separate package `RazorEngine.Roslyn` (last 4.1.1-rc1); the fork contains the never-released upstream change from 3.10.1-alpha001 that merged it into the main assembly, so `RoslynCompilerServiceFactory` is available without another package. Remove `RazorEngine.Roslyn` when switching. CodeDom (`csc.exe`, C# 5) stays the default compiler in 4.0.0.
 * (PACKAGING) Because of that, the package now depends on Microsoft.CodeAnalysis.CSharp 1.3.2, System.Collections.Immutable 1.3.1 and System.Reflection.Metadata 1.4.2 in addition to Microsoft.AspNet.Razor (the original 3.10.0 only depended on Razor).
 * (NEW) Values in a `DynamicViewBag` are exchanged across `AppDomain`s, so a view bag works with `IsolatedRazorEngineService` (never-released upstream change, thanks campersau, https://github.com/Antaris/RazorEngine/pull/504).
 * (BREAKING) Target .NET Framework 4.8 only. The net40 (Razor 2) and Razor4 (Microsoft.AspNetCore.Razor) builds are dropped.
 * (BUILD) SDK-style projects with NuGet PackageReference replace Paket and FAKE. Build with `dotnet build`, test with `dotnet test`, pack with `dotnet pack`.
 * (BUILD) GitHub Actions replace Travis CI and AppVeyor.
 * (PACKAGING) The package has the `Apache-2.0` license expression, contains README and LICENSE, comes with a symbol package (`.snupkg`) on nuget.org and is built deterministically on GitHub Actions.
 * (BUILD) Releases are published from GitHub Actions: pushing a version tag builds, tests and packs that commit, pushes the package to nuget.org with trusted publishing and creates the GitHub release.
 * (BREAKING) Requires Microsoft.AspNet.Razor 3.3.0 (3.x maintenance release, the parser is unchanged since 3.2.3, so template syntax is unchanged from the 3.x net45 build). ASP.NET MVC 5.2.x only allows Razor below 3.3.0: apps using MVC have to update to Microsoft.AspNet.Mvc 5.3.0 (and Microsoft.AspNet.WebPages 3.3.0) to install this package, otherwise NuGet reports a version conflict (NU1107).
 * (PACKAGING) Depend on System.Security.Cryptography.X509Certificates 4.3.2 instead of the 4.1.0 pulled in by Roslyn. NuGet audit flags 4.1.0 for CVE-2017-11770, which only affects .NET Core, not .NET Framework; the pin keeps the audit warning away from consumers.
 * (BUILD) Tests use NUnit 5 (constraint model asserts), Moq 4.21 and Autofac 9; RazorEngine.Mvc uses Microsoft.AspNet.Mvc 5.3.0. Package versions are managed centrally in `src/Directory.Packages.props`.

### 3.10.1-alpha001 / 4.5.1-alpha001

 * (NEW) Merge RazorEngine.Roslyn into RazorEngine.Core, thanks conniey (https://github.com/Antaris/RazorEngine/pull/480)

### 3.10.0 / 4.5.0-rc1

 * (NEW) Add a simple way to create typed, runnable templates, thanks ocoanet (https://github.com/Antaris/RazorEngine/pull/431)
 * (NEW) Adding in temporary file path configuration, thanks brentonlamar (https://github.com/Antaris/RazorEngine/pull/400)
 * (DOCS) Fix typo in README.md, thanks a510 (https://github.com/Antaris/RazorEngine/pull/435)
 * (ENHANCEMENT) Add FullName to Exception when Assembly is not found, thanks zamojski (https://github.com/Antaris/RazorEngine/pull/448)
 * (BUGFIX) Fix RazorDynamicObject dispose automatically created RazorDynamicObjects, thanks campersau (https://github.com/Antaris/RazorEngine/pull/438)
 * (BUILD) Updates NUnit to 3.6.1, thanks conniey (https://github.com/Antaris/RazorEngine/pull/465)

### 3.9.3 / 4.4.3-rc1

 * (NEW) Added Support for .vbhtml files in ResolvePathTemplateManager, thanks AnderssonPeter (https://github.com/Antaris/RazorEngine/pull/426)

### 3.9.2 / 4.4.2-rc1

 * (PACKAGING) Fix version constraint, thanks campersau (https://github.com/Antaris/RazorEngine/pull/429)

### 3.9.1 / 4.4.1-rc1

 * (BUGFIX) dispose ReaderWriterLockSlim lock in ReferencesListForDynamicAssemblyResolution, thanks campersau (https://github.com/Antaris/RazorEngine/pull/411)
 * (DOCS) update copyright year, thanks campersau (https://github.com/Antaris/RazorEngine/pull/412)
 * (DOCS) Update template.cshtml, thanks (https://github.com/Antaris/RazorEngine/pull/413)
 
### 3.9.0 / 4.4.0-rc1

 * Fix memory leak in CompilerServiceBase by removing event handler in dispose, thanks campersau (https://github.com/Antaris/RazorEngine/pull/388)
 * support AspNetCore.Razor 1.0.0, thanks campersau (https://github.com/Antaris/RazorEngine/pull/402)
 
### 3.8.2 / 4.3.2-beta1

 * support models which implement ISerializable with IsolatedRazorEngineService (https://github.com/Antaris/RazorEngine/pull/376)
 * fix vb.net generic type parameter list (https://github.com/Antaris/RazorEngine/pull/375)
 * Added documentation for EmbeddedResourceTemplateManager (https://github.com/Antaris/RazorEngine/pull/374)

### 3.8.1 / 4.3.1-beta1

 * Added EmbeddedResource Template Manager (https://github.com/Antaris/RazorEngine/pull/371)
 * (breaking) Renamed optional isRequired parameter of RenderSection method to required (https://github.com/Antaris/RazorEngine/pull/372)
 * Use file modification time to check if cached template is valid (https://github.com/Antaris/RazorEngine/pull/353)
   Contains InvalidatingByModifiedTimeCachingProvider and ResolvePathCheckModifiedTimeTemplateManager.

### 3.8.0 / 4.3.0-beta1

 * See 3.8.1. Rereleased as a change was missing in the release notes.

### 3.7.7 / 4.2.7-beta1

 * Fixed bug, thanks @Greooo (https://github.com/Antaris/RazorEngine/issues/244#issuecomment-173322177)

### 3.7.6 / 4.2.6-beta1

 * Fix https://github.com/Antaris/RazorEngine/pull/348

### 3.7.5 / 4.2.5-beta1

 * Fix https://github.com/Antaris/RazorEngine/issues/275
 * Fix https://github.com/Antaris/RazorEngine/issues/343

### 3.7.4 / 4.2.4-beta1

 * make implementation thread-safe, fixes #325 and #331. https://github.com/Antaris/RazorEngine/pull/339
 * add documentation for partial templates and layouts (https://github.com/Antaris/RazorEngine/issues/338)
 * fix a serialization bug when using the isolation API on mono for current versions (NOTE: this version is no longer compatible with older mono versions in isolation scenarios, because an older workaround was removed).

### 3.7.3 / 4.2.3-beta1

 * Template Run performance optimizations (https://github.com/Antaris/RazorEngine/pull/328)
 * Update to Razor4-beta7 (https://github.com/Antaris/RazorEngine/pull/319)

### 3.7.2 / 4.2.2-beta1

 * Validate and safe-copy the configuration. Changing the configuration after creating a RazorEngineService instance is not (and was never) supported.

### 3.7.1 / 4.2.1-beta1

 * Validate and safe-copy the configuration. Changing the configuration after creating a RazorEngineService instance is not (and was never) supported.

### 3.7.1-beta1 / 4.2.0-beta3

 * Build version 4 against Mircrosoft.AspNet.Razor 4.

### 3.7.0 / 4.2.0-beta2

 * add InvalidatingCachingProvider, ResolvePathTemplateManager and WatchingResolvePathTemplateManager, fixes https://github.com/Antaris/RazorEngine/issues/250
 * Switched to Apache 2: https://github.com/Antaris/RazorEngine/issues/190
 * Missing feature (compared to 3.6): Configuration fallback to XML.

### 3.6.6 / 4.1.6-beta1
* Another attempt to fix https://github.com/Antaris/RazorEngine/issues/267

### 3.6.5 / 4.1.5-beta1
* Fix https://github.com/Antaris/RazorEngine/issues/267

### 3.6.4 / 4.1.4-beta1
* Use /nostdlib when we find a mscorlib (improves mono support)
* Added `DisableTempFileLocking` to load assemblies in memory (to prevent temp file locking), 
  this is only recommended in a very limited amount of scenarios.
  Please be aware of the consequences before using it.
  Please read https://github.com/Antaris/RazorEngine/issues/244 for details.
  Note that this can introduce memory leaks to your application.

### 3.6.3 / 4.1.3-beta2
* Cleanup temporary files when RazorEngine is not used in the default AppDomain
* Write to stderr when cleanup is not successful.
* Add an API to change the temporary directory (by subclassing CompilerService)
* Fix https://github.com/Antaris/RazorEngine/issues/253

### 3.6.3-beta2
* Fix some race conditions in cleanup.
* Fix https://github.com/Antaris/RazorEngine/issues/253

### 3.6.3-beta1 / 4.1.3-beta1
* Cleanup temporary files when RazorEngine is not used in the default AppDomain
* Add an API to change the temporary directory (by subclassing CompilerService)

### 3.6.2 / 4.1.2-beta1
* Add support for escaping the dynamic wrapper in certain situations
  - Implicit or explicit casts
  - By using `RazorEngine.Compilation.RazorDynamicObject.Unwrap(Model.ToUnwrap)`
* Wrapper now supports binary and unary operations (thanks devteamexpress): https://github.com/Antaris/RazorEngine/issues/248

### 3.6.1 / 4.1.1-beta1
* Add support for nested model types as well
* Fix a bug with non-generic nested template base classes.

### 3.6.0 / 4.1.0-beta1
* ICodeInspector API is now obsolete (it has been removed in 4.0.0).
* Nested classes can now be used as template-base-class.
* ViewBag data can now be used in overwritten SetModel calls
  Viewbag is set like the model on template creation (not in the ExecuteContext)
  This leads to some minor breaking changes
  - Creating a ExecuteContext with a non-null viewbag throws a NotSupportedException (obsolete API)
  - added a DynamicViewBag parameter to IInternalTemplateService.Resolve (you should not need to use that API)
  - ITemplate.SetModel has changed to ITemplate.SetData (you are not affected if you use TemplateBase or TemplateBase<T> as base class, which is recommended)
  
These changes are strictly speaking breaking, but they shouldn't practically affect anyone.
Watch out for new Obsolete warnings and fix them.

### 3.5.3 / 4.0.3-beta1
* Fixed a SecurityException when the template is broken.
* RazorEngine is now compatible with ImpromptuInterface (thanks kipwilliams).

### 3.5.2 / 4.0.2-beta1
* If you use assemblies in your template which are not referenced in your hosting code 
  RazorEngine will load them for you automatically if they are required.
  See https://github.com/Antaris/RazorEngine/issues/218 for details.

### 3.5.1 / 4.0.1-beta1
* Fixed https://github.com/Antaris/RazorEngine/issues/217

### 3.5.0 / 4.0.0-beta2
* documentation website based on FSharp.Formatting (https://Antaris.github.io/RazorEngine), can be build locally
* travis and appveyor build systems to automatically build on mono and windows.
* build environment to build and release the project, the documentation and the nuget package.
* readded support for net40 (which will use the Razor with version 2.0.30506.0)
* New API surface (various classes are Obsolete now).
* Improved Isolation (AppDomain Sandboxing) support.
* Caching API.
* Fixed most open bugs.
* Razor 4 support (in 4.0.0)
* Roslyn Support (via RazorEngine.Roslyn, not supported on net40)
* Please report breaking changes in 3.5.0 (from 3.4.x) and open an issue!

### 3.5.0-beta3 / 4.0.0-beta2
* Fix a missing [SecurityCritical] attribute on TemplateParsingException.GetObjectData

### 3.5.0-beta2
* Relax nuget Razor dependency

### New in 3.5.0-beta1 / 4.0.0-beta1 (To be released) 
* documentation website based on FSharp.Formatting (https://Antaris.github.io/RazorEngine), can be build locally
* travis and appveyor build systems to automatically build on mono and windows.
* build environment to build and release the project, the documentation and the nuget package.
* readded support for net40 (which will use the Razor with version 2.0.30506.0)
* New API surface (various classes are Obsolete now).
* Improved Isolation (AppDomain Sandboxing) support.
* Caching API.
* Fixed most open bugs.
* Razor 4 support (in 4.0.0)
* Roslyn Support (via RazorEngine.Roslyn, not supported on net40)
* Please report breaking changes in 3.5.0-beta1 (from 3.4.x) and open an issue!
