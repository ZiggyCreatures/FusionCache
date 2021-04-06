using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

#if NETSTANDARD2_1
using System.Runtime.Loader;
#endif
using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion
{
    /// <summary>
    /// Simple assembly scanner for finding a <see cref="IFusionMetrics"/> plugin.
    /// </summary>
    public class MetricsPluginLoader
    {
        /// <summary>
        /// Creates a new scanner defaulting to the <see cref="AppDomain.CurrentDomain"/>.BaseDirectory
        /// </summary>
        public MetricsPluginLoader()
            : this(AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        /// <summary>
        /// Creates a new scanner with an alternative directory from the default <see cref="AppDomain.CurrentDomain"/>.BaseDirectory
        /// </summary>
        /// <param name="scanDirectory"></param>
        public MetricsPluginLoader(string scanDirectory)
        {
            _scanDirectory = scanDirectory;
        }

        private readonly string _scanDirectory;

        /// <summary>
        /// Find a single IFusionMetrics plugin.
        /// </summary>
        /// <returns></returns>
        public IFusionMetrics? GetPlugin(string cacheName, IMemoryCache? cache)
        {
            var assemblies = new List<Assembly>();
            
            foreach (var assemblyFile in ScanDirectoryForAssemblyFiles(_scanDirectory))
            {
                if (TryLoadAssemblyPlugin(assemblyFile.FullName, out var assembly))
                {
                    assemblies.Add(assembly);
                }
            }

            if (!assemblies.Any())
            {
                return null;
            }
            
            if (assemblies.Count > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(IFusionMetrics),
                    string.Join(",", assemblies.Select(a => a.FullName)),
                    "Only one IFusionMetrics plugin allowed.");
            }

            var metrics = (IFusionMetrics)Activator.CreateInstance(
                assemblies.Single().GetTypes().Single(t => typeof(IFusionMetrics).IsAssignableFrom(t)
                                                           && !t.IsInterface),
                cacheName,
                cache);

            return metrics;
        }

        private bool TryLoadAssemblyPlugin(string path, out Assembly? assembly)
        {
            assembly = null;

            if (path.Contains("ZiggyCreatures.FusionCache.EventCounters.dll"))
            {
                var joe = "stuff";
            }
            
            try
            {
#if NETSTANDARD2_1
                var context = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
                assembly = context.LoadFromAssemblyPath(path);
#else
                assembly = Assembly.LoadFrom(path);
#endif

                if (assembly.GetTypes().Any(t => typeof(IFusionMetrics).IsAssignableFrom(t)
                                                                       && !t.IsInterface))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex) 
                // when (ex is BadImageFormatException || ex is FileLoadException || ex is ReflectionTypeLoadException)
            {
                return false;
            }
        }

        static List<FileInfo> ScanDirectoryForAssemblyFiles(string scanDirectory)
        {
            var fileInfo = new List<FileInfo>();
            var baseDir = new DirectoryInfo(scanDirectory);
            
            foreach (var info in baseDir.GetFiles("*.dll", SearchOption.TopDirectoryOnly))
            {
                fileInfo.Add(info);
            }
            
            return fileInfo;
        }
    }
}
