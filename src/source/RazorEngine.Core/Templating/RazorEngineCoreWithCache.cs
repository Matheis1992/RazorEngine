using RazorEngine.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RazorEngine.Templating
{
    internal class RazorEngineCoreWithCache : RazorEngineCore
    {
        /// <summary>
        /// The compiles that are running, per template key and model type, so that threads that miss the cache at the same time
        /// compile a template only once. An entry exists only while its compile runs. Different templates still compile in parallel.
        /// </summary>
        private readonly ConcurrentDictionary<Tuple<string, Type>, Lazy<ICompiledTemplate>> _runningCompiles =
            new ConcurrentDictionary<Tuple<string, Type>, Lazy<ICompiledTemplate>>();

        internal RazorEngineCoreWithCache(ReadOnlyTemplateServiceConfiguration config, RazorEngineService cached)
            : base(config, cached)
        {
        }

        /// <summary>
        /// Returns the cached template, or compiles and caches it on a cache miss.
        /// Concurrent callers for the same key and model type wait for the running compile and get its result, or its exception
        /// if it fails; the next caller after a failed compile compiles again.
        /// </summary>
        internal ICompiledTemplate GetOrCompile(ITemplateKey key, Type modelType)
        {
            var cachingProvider = Configuration.CachingProvider;
            ICompiledTemplate compiledTemplate;
            if (cachingProvider.TryRetrieveTemplate(key, modelType, out compiledTemplate))
            {
                return compiledTemplate;
            }

            var compileKey = Tuple.Create(key.GetUniqueKeyString(), InvalidatingCachingProvider.GetModelTypeKey(modelType));
            var compile = _runningCompiles.GetOrAdd(compileKey, _ => new Lazy<ICompiledTemplate>(() =>
            {
                // A compile that finished just before this one started has already cached the template.
                ICompiledTemplate compiled;
                if (!cachingProvider.TryRetrieveTemplate(key, modelType, out compiled))
                {
                    compiled = Compile(key, modelType);
                    cachingProvider.CacheTemplate(compiled, key);
                }
                return compiled;
            }));
            try
            {
                return compile.Value;
            }
            finally
            {
                // Remove this compile (not one started later): the result is cached, and after a failure the next caller compiles again.
                ((ICollection<KeyValuePair<Tuple<string, Type>, Lazy<ICompiledTemplate>>>)_runningCompiles)
                    .Remove(new KeyValuePair<Tuple<string, Type>, Lazy<ICompiledTemplate>>(compileKey, compile));
            }
        }

        internal override ITemplate ResolveInternal(string cacheName, object model, Type modelType, DynamicViewBag viewbag, ResolveType resolveType, ITemplateKey context)
        {
            var templateKey = GetKey(cacheName, resolveType, context);
            var compiledTemplate = GetOrCompile(templateKey, modelType);
            return CreateTemplate(compiledTemplate, model, viewbag);
        }
    }
}
