using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Services.Data.Base;

namespace Kitsune.SDK.Extensions.Player
{
    /// <summary>
    /// Helper methods for player handling - centralizes duplicated code from storage and settings extensions
    /// </summary>
    internal static class PlayerHandlerHelpers
    {
        // Map of call contexts to plugins - we'll use callsite-specific data instead of a global AsyncLocal
        // This allows different plugins to have their own context in the shared library
        private static readonly ConcurrentDictionary<int, WeakReference<BasePlugin>> _pluginContextMap = new();

        /// <summary>
        /// Find a handler by key, with optimized plugin lookup
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (THandler? Handler, string Key) FindHandler<THandler>(string key, BasePlugin? currentPlugin, Dictionary<BasePlugin, THandler> handlers, ConcurrentDictionary<string, BasePlugin> pluginNameCache, ConcurrentDictionary<string, BasePlugin> keyToPlugin, Func<THandler, string> getOwnerPlugin) where THandler : class
        {
            // Fast path: key contains explicit plugin (cross-plugin access)
            if (key.Contains(':'))
            {
                int colonIndex = key.IndexOf(':');
                string pluginName = key.Substring(0, colonIndex);
                string actualKey = key.Substring(colonIndex + 1);

                // Try plugin cache first
                if (pluginNameCache.TryGetValue(pluginName, out var plugin) && handlers.TryGetValue(plugin, out var handler))
                {
                    return (handler, actualKey);
                }

                // Cache miss - need to find the handler
                foreach (var pair in handlers)
                {
                    if (getOwnerPlugin(pair.Value) == pluginName)
                    {
                        // Cache for future lookups
                        pluginNameCache[pluginName] = pair.Key;
                        return (pair.Value, actualKey);
                    }
                }

                return (null, actualKey);
            }

            // Check if current plugin is registered (if we have one)
            if (currentPlugin != null && handlers.TryGetValue(currentPlugin, out var currentHandler))
            {
                // Look for keys registered with the current plugin first
                string fullKey = $"{getOwnerPlugin(currentHandler)}:{key}";
                if (keyToPlugin.TryGetValue(fullKey, out _))
                {
                    return (currentHandler, key);
                }
            }

            // Try any handler's registered keys
            foreach (var pair in handlers)
            {
                string testKey = $"{getOwnerPlugin(pair.Value)}:{key}";
                if (keyToPlugin.ContainsKey(testKey))
                {
                    return (pair.Value, key);
                }
            }

            // Default to first registered handler if it exists
            return handlers.Count > 0 ? (handlers.Values.First(), key) : (null, key);
        }

        /// <summary>
        /// Reset data for a specific scope
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Task ResetDataAsync<THandler>(string scope, ulong steamId, Dictionary<BasePlugin, THandler> handlers, Func<THandler, string> getOwnerPlugin, Func<THandler, ulong, Task> resetAction) where THandler : class
        {
            if (string.IsNullOrEmpty(scope))
                throw new ArgumentNullException(nameof(scope), "Plugin name must be specified");

            if (scope == "*")
            {
                // Reset all handlers
                return Task.WhenAll(handlers.Values.Select(h => resetAction(h, steamId)));
            }

            // Find specific handler for the scope
            var handler = handlers.Values.FirstOrDefault(h => getOwnerPlugin(h) == scope) ?? throw new InvalidOperationException($"No handler found for plugin '{scope}'");
            return resetAction(handler, steamId);
        }

        /// <summary>
        /// Set current plugin context for a specific plugin instance. Rather than using a global
        /// async context, we use the plugin's hash code to create a context-specific mapping.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetPluginContext(SdkPlugin plugin)
        {
            // Store a weak reference to avoid memory leaks
            _pluginContextMap[plugin.GetHashCode()] = new WeakReference<BasePlugin>(plugin);
        }

        /// <summary>
        /// Get the plugin context for a specific hash code
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BasePlugin? GetPluginContext(int pluginHashCode)
        {
            if (_pluginContextMap.TryGetValue(pluginHashCode, out var weakRef) && weakRef.TryGetTarget(out var plugin))
            {
                return plugin;
            }

            return null;
        }

        /// <summary>
        /// Find a plugin in the call stack by examining stack frames
        /// </summary>
        internal static BasePlugin? FindPluginInCallStack()
        {
            // Try to get from thread-local context first (fastest path)
            var contextPlugin = GetCurrentThreadPlugin();
            if (contextPlugin != null)
                return contextPlugin;

            // Fall back to stack trace only if necessary
            var stackFrames = new StackTrace(skipFrames: 2, fNeedFileInfo: false).GetFrames();
            if (stackFrames != null)
            {
                // Cache for type lookups
                var typeCache = GetTypeCache();

                foreach (var frame in stackFrames)
                {
                    var method = frame.GetMethod();
                    var declaringType = method?.DeclaringType;

                    if (declaringType == null)
                        continue;

                    // Check if we have this type cached
                    if (typeCache.TryGetValue(declaringType, out var cachedPlugin))
                        return cachedPlugin;

                    // Check if it's a SdkPlugin subclass
                    if (declaringType.IsSubclassOf(typeof(SdkPlugin)))
                    {
                        var plugin = GetAllPlugins().FirstOrDefault(p => p.GetType() == declaringType);
                        if (plugin != null)
                        {
                            // Cache this type for future lookups
                            typeCache.TryAdd(declaringType, plugin);
                            return plugin;
                        }
                    }
                }
            }

            // Last resort: return first available plugin
            return GetAllPlugins().FirstOrDefault();
        }

        // Thread-local storage for current plugin context
        [ThreadStatic]
        private static BasePlugin? _currentThreadPlugin;

        // Type to plugin cache
        private static readonly ConcurrentDictionary<Type, BasePlugin> _typeToPluginCache = new();

        /// <summary>
        /// Set the current thread's plugin context
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetCurrentThreadPlugin(BasePlugin? plugin)
        {
            _currentThreadPlugin = plugin;
        }

        /// <summary>
        /// Get the current thread's plugin context
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BasePlugin? GetCurrentThreadPlugin()
        {
            return _currentThreadPlugin;
        }

        /// <summary>
        /// Get the type to plugin cache
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ConcurrentDictionary<Type, BasePlugin> GetTypeCache()
        {
            return _typeToPluginCache;
        }

        /// <summary>
        /// Gets all plugins currently loaded
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IEnumerable<BasePlugin> GetAllPlugins()
        {
            // Return plugins from handlers and clean up stale references
            var plugins = new HashSet<BasePlugin>();
            var staleKeys = new List<int>();

            // Add all plugins that have registered handlers
            foreach (var handler in PlayerDataHandler.GetAllHandlers())
            {
                if (handler.Plugin != null)
                {
                    plugins.Add(handler.Plugin);
                }
            }

            // Clean up plugin context map - remove any stale entries
            foreach (var kvp in _pluginContextMap)
            {
                if (!kvp.Value.TryGetTarget(out var plugin) || plugin == null)
                {
                    staleKeys.Add(kvp.Key);
                }
                else
                    plugins.Add(plugin);
            }

            // Remove stale references
            foreach (var key in staleKeys)
            {
                _pluginContextMap.TryRemove(key, out _);
            }

            return plugins;
        }
    }
}