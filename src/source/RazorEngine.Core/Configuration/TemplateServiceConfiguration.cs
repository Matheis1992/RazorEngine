namespace RazorEngine.Configuration
{
    using Compilation;
    using RazorEngine.Compilation.ReferenceResolver;
    using System;
    using System.Collections.Generic;
    using System.Security;
    using Templating;
    using Text;

    /// <summary>
    /// Provides a default implementation of a template service configuration.
    /// </summary>
    public class TemplateServiceConfiguration : ITemplateServiceConfiguration
    {
        #region Constructor

        /// <summary>
        /// Initialises a new instance of <see cref="TemplateServiceConfiguration"/>.
        /// </summary>
        [SecuritySafeCritical]
        public TemplateServiceConfiguration()
        {
            Activator = new DefaultActivator();
            CompilerServiceFactory =
                new DefaultCompilerServiceFactory();
            EncodedStringFactory = new HtmlEncodedStringFactory();
            ReferenceResolver = new UseCurrentAssembliesReferenceResolver();
            CachingProvider = new DefaultCachingProvider();
            TemplateManager =
                new DelegateTemplateManager();

            Namespaces = new HashSet<string>
                             {
                                 "System", 
                                 "System.Collections.Generic", 
                                 "System.Linq"
                             };
            
            var config = RazorEngineConfigurationSection.GetConfiguration();
            Language = (config == null)
                           ? Language.CSharp
                           : config.DefaultLanguage;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the activator.
        /// </summary>
        public IActivator Activator { get; set; }

        /// <summary>
        /// Gets or sets whether to allow missing properties on dynamic models.
        /// </summary>
        public bool AllowMissingPropertiesOnDynamic { get; set; }

        /// <summary>
        /// Loads all dynamic assemblies with Assembly.Load(byte[]).
        /// This prevents temp files from being locked (which makes it impossible for RazorEngine to delete them).
        /// At the same time this completely shuts down any sandboxing/security.
        /// Use this only if you have a limited amount of static templates (no modifications on rumtime), 
        /// which you fully trust and when a seperate AppDomain is no solution for you!.
        /// This option will also hurt debugging.
        /// 
        /// OK, YOU HAVE BEEN WARNED.
        /// </summary>
        public bool DisableTempFileLocking { get; set; }

        /// <summary>
        /// Gets or sets the base template type.
        /// </summary>
        public Type BaseTemplateType { get; set; }

        /// <summary>
        /// Gets or sets the reference resolver
        /// </summary>
        public IReferenceResolver ReferenceResolver { get; set; }

        /// <summary>
        /// Gets or sets the caching provider.
        /// </summary>
        public ICachingProvider CachingProvider { get; set; }

        /// <summary>
        /// Gets or sets the compiler service factory.
        /// </summary>
        public ICompilerServiceFactory CompilerServiceFactory { get; set; }

        /// <summary>
        /// Gets whether the template service is operating in debug mode.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets the encoded string factory.
        /// </summary>
        public IEncodedStringFactory EncodedStringFactory { get; set; }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        public Language Language { get; set; }

        /// <summary>
        /// Gets or sets the collection of namespaces.
        /// </summary>
        public ISet<string> Namespaces { get; set; }

        /// <summary>
        /// Gets or sets the template manager.
        /// </summary>
        public ITemplateManager TemplateManager { get; set; }
        #endregion
    }
}