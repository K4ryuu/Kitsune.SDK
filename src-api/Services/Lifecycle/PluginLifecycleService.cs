using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Base;

namespace Kitsune.SDK.Services
{
    /// <summary>
    /// Disposal priority levels
    /// </summary>
    public enum DisposalPriority
    {
        First = 0,      // Dispose first (e.g., Players)
        Normal = 100,   // Default priority
        Late = 200,     // Dispose late (e.g., Storage/Settings handlers)
        Last = 300      // Dispose last (e.g., ConfigHandler)
    }

    /// <summary>
    /// Central service for managing the complete lifecycle of Zenith plugins
    /// </summary>
    public sealed class PluginLifecycleService : IDisposable
    {
        // Single collection for resource tracking - optimized for game performance
        private static readonly ConcurrentDictionary<BasePlugin, PluginResources> _pluginResources = new();

        // The plugin instance this service manages
        private readonly BasePlugin _plugin;

        // Logger for lifecycle events
        private readonly ILogger _logger;

        // Resource container for this plugin
        private readonly PluginResources _resources;

        // Whether this instance has been disposed
        private bool _disposed;

        /// <summary>
        /// Efficient container for plugin-specific resources
        /// </summary>
        private sealed class PluginResources
        {
            // Using lists for better performance and iteration speed
            public readonly List<Action> CleanupActions = [];
            public readonly List<Func<Task>> AsyncCleanupActions = [];

            // Priority-based disposal order
            public readonly SortedDictionary<int, List<IDisposable>> PrioritizedDisposables = new();

            // Lock for thread-safe modifications
            public readonly object ResourceLock = new();
        }

        /// <summary>
        /// Creates a new lifecycle service for the specified plugin
        /// </summary>
        /// <param name="plugin">The plugin to manage</param>
        public PluginLifecycleService(BasePlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _logger = plugin.Logger;

            // Create or get existing registry for this plugin
            _resources = _pluginResources.GetOrAdd(_plugin, _ => new PluginResources());
        }

        /// <summary>
        /// Registers a disposable resource to be cleaned up on plugin unload
        /// </summary>
        /// <typeparam name="T">Type of the resource</typeparam>
        /// <param name="resource">The resource to register</param>
        /// <returns>The same resource for chaining</returns>
        public T RegisterResource<T>(T resource) where T : class
        {
            return RegisterResource(resource, DisposalPriority.Normal);
        }

        /// <summary>
        /// Registers a disposable resource with priority
        /// </summary>
        /// <typeparam name="T">Type of the resource</typeparam>
        /// <param name="resource">The resource to register</param>
        /// <param name="priority">The disposal priority</param>
        /// <returns>The same resource for chaining</returns>
        public T RegisterResource<T>(T resource, DisposalPriority priority) where T : class
        {
            ArgumentNullException.ThrowIfNull(resource);
            if (_disposed) throw new ObjectDisposedException(nameof(PluginLifecycleService));

            // Register as IDisposable if applicable
            if (resource is IDisposable disposable)
            {
                lock (_resources.ResourceLock)
                {
                    // Add to priority queue
                    if (!_resources.PrioritizedDisposables.TryGetValue((int)priority, out var list))
                    {
                        list = new List<IDisposable>();
                        _resources.PrioritizedDisposables[(int)priority] = list;
                    }
                    list.Add(disposable);
                }
            }

            return resource;
        }

        /// <summary>
        /// Registers a cleanup action to be executed on plugin unload
        /// </summary>
        /// <param name="cleanupAction">The action to execute</param>
        public void RegisterCleanupAction(Action cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(cleanupAction);
            if (_disposed) throw new ObjectDisposedException(nameof(PluginLifecycleService));

            lock (_resources.ResourceLock)
            {
                _resources.CleanupActions.Add(cleanupAction);
            }
        }

        /// <summary>
        /// Registers an async cleanup action to be executed on plugin unload
        /// </summary>
        /// <param name="cleanupAction">The async action to execute</param>
        public void RegisterAsyncCleanupAction(Func<Task> cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(cleanupAction);
            if (_disposed) throw new ObjectDisposedException(nameof(PluginLifecycleService));

            lock (_resources.ResourceLock)
            {
                _resources.AsyncCleanupActions.Add(cleanupAction);
            }
        }

        /// <summary>
        /// Disposes all resources for a plugin synchronously with priority order
        /// </summary>
        public void DisposePlugin()
        {
            // Local copies for thread-safety during execution
            List<Action> cleanupActions;
            SortedDictionary<int, List<IDisposable>> prioritizedDisposables;

            lock (_resources.ResourceLock)
            {
                cleanupActions = [.. _resources.CleanupActions];
                // Create a deep copy of the prioritized disposables
                prioritizedDisposables = new SortedDictionary<int, List<IDisposable>>();
                foreach (var kvp in _resources.PrioritizedDisposables)
                {
                    prioritizedDisposables[kvp.Key] = [.. kvp.Value];
                }
            }

            // Run cleanup actions first
            foreach (var action in cleanupActions)
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing cleanup action");
                }
            }

            // Dispose resources in priority order (lowest number = dispose first)
            foreach (var kvp in prioritizedDisposables)
            {
                var priority = kvp.Key;
                var disposables = kvp.Value;

                _logger.LogDebug("Disposing {Count} resources at priority {Priority}", disposables.Count, priority);

                foreach (var resource in disposables)
                {
                    try
                    {
                        _logger.LogDebug("Disposing {ResourceType}", resource.GetType().Name);
                        resource.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing resource: {ResourceType}", resource.GetType().Name);
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronously disposes all resources for a plugin
        /// </summary>
        public async Task DisposePluginAsync()
        {
            // Make local copies for thread safety
            List<Func<Task>> asyncCleanupActions;

            lock (_resources.ResourceLock)
            {
                asyncCleanupActions = [.. _resources.AsyncCleanupActions];
            }

            // Run async cleanup actions first
            foreach (var action in asyncCleanupActions)
            {
                try
                {
                    await action.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing async cleanup action");
                }
            }

            // Run sync cleanup actions to dispose regular resources
            DisposePlugin();
        }

        /// <summary>
        /// Performs full cleanup for a plugin, removing it from global registry
        /// </summary>
        private void CleanupPlugin()
        {
            if (_pluginResources.TryRemove(_plugin, out var resources))
            {
                lock (resources.ResourceLock)
                {
                    resources.CleanupActions.Clear();
                    resources.AsyncCleanupActions.Clear();
                    resources.PrioritizedDisposables.Clear();
                }
            }

            // Clean up player-related keys for this plugin
            Player.CleanupPluginKeys(_plugin);
        }

        /// <summary>
        /// Disposes this instance and cleans up all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            DisposePlugin();
            CleanupPlugin();

            _disposed = true;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Ensures resources are properly disposed if this instance is garbage collected
        /// </summary>
        ~PluginLifecycleService()
        {
            if (!_disposed)
            {
                DisposePlugin();
                CleanupPlugin();
            }
        }
    }
}