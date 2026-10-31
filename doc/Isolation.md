
# RazorEngine Isolation API

`IsolatedRazorEngineService` compiles and runs templates in a separate, fully trusted `AppDomain`.
Assemblies loaded into an `AppDomain` can only be released by unloading the whole `AppDomain`,
so this is the way to free the memory of compiled templates (for example after templates changed at runtime)
and to let RazorEngine clean up its temporary files.

All you need to do is use `IsolatedRazorEngineService.Create` instead of `RazorEngineService.Create`.
Disposing the service unloads the `AppDomain` together with all templates compiled in it:

```csharp
using (var service = IsolatedRazorEngineService.Create())
{
    string result = service.RunCompile(template, "templateKey", typeof(MyModel), model);
} // the AppDomain and all compiled templates are unloaded here
```

You can provide your own `IAppDomainFactory` or use the `Func<AppDomain>` overloads of `IsolatedRazorEngineService.Create`
to control how the `AppDomain` is created, as long as it is fully trusted.
Pass your application's configuration file to the new `AppDomain`, so its binding redirects apply there too (needed for Roslyn's dependencies if you use RazorEngine.NetFramework.Roslyn):

```csharp
var setup = new AppDomainSetup
{
    ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
    ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
};
using (var service = IsolatedRazorEngineService.Create(() => AppDomain.CreateDomain("Templates", null, setup)))
{
    ...
}
```

Without it, compiling fails with a `FileLoadException` for `System.Collections.Immutable` or a similar assembly.
The default `IsolatedRazorEngineService.Create()` already does this.

## Isolation is no security boundary

Templates are C# code and run with the full rights of your application.
**Do not run templates from untrusted sources**, not even with isolation.

Earlier versions supported running templates in a partially trusted `AppDomain` (a Code Access Security sandbox).
This is not supported anymore: CAS is obsolete and no security boundary, and templates can escape such a sandbox
([CVE-2021-46703](https://github.com/advisories/GHSA-ph3v-2hq5-5qfq)).
`IsolatedRazorEngineService` throws a `NotSupportedException` when the
application domain factory returns a partially trusted `AppDomain`.

If you have to run templates written by untrusted users, isolate them outside of RazorEngine,
for example in a separate process running under an account with minimal rights, or in a container.

## SerializationException

Whenever you are in an isolation scenario all your models need to traverse into another AppDomain, that means your model needs to be serializable!
When you call methods on your model and you use an MarshalByRef scenario all your parameters and return types must be serializable as well!

If you want to use a model instance which is not serializable you can use `dynamic` and wrap it in an `RazorDynamicObject` 
this should magically take care of everything for you.
The only thing not supported is to cast the model within the template in its type (all interfaces will work however).
You can even call methods with serializable parameters (return type must not be serializable).

## Configuration

RazorEngine provides an `IConfigCreator` interface to configure an `IsolatedRazorEngineService`.

```csharp
/// <summary>
/// A helper interface to get a custom configuration into a new AppDomain.
/// Classes inheriting this interface should be Serializable 
/// (and not inherit from MarshalByRefObject).
/// </summary>
public interface IConfigCreator
{
    /// <summary>
    /// Create a new configuration instance.
    /// This method should be executed in the new AppDomain.
    /// </summary>
    /// <returns></returns>
    ITemplateServiceConfiguration CreateConfiguration();
}

/// <summary>
/// A simple <see cref="IConfigCreator"/> implementation to configure the <see cref="Language"/> and the <see cref="Encoding"/>.
/// </summary>
[Serializable]
public class LanguageEncodingConfigCreator : IConfigCreator
{
    private Language language;
    private Encoding encoding;

    /// <summary>
    /// Initializes a new <see cref="LanguageEncodingConfigCreator"/> instance
    /// </summary>
    /// <param name="language"></param>
    /// <param name="encoding"></param>
    public LanguageEncodingConfigCreator(Language language = Language.CSharp, Encoding encoding = Encoding.Html)
    {
        this.language = language;
        this.encoding = encoding;
    }

    /// <summary>
    /// Create the configuration.
    /// </summary>
    /// <returns></returns>
    public ITemplateServiceConfiguration CreateConfiguration()
    {
        return new TemplateServiceConfiguration()
        {
            Language = language,
            EncodedStringFactory = RazorEngineService.GetEncodedStringFactory(encoding)
        };
    }
}
```

The only thing to mention here is that the implementation must be serializable but not inherit from `MarshalByRefObject`.
