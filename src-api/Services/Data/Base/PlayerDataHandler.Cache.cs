using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Services.Data.Cache.Keys;
using Kitsune.SDK.Services.Data.Storage;
using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Services.Config;

namespace Kitsune.SDK.Services.Data.Base
{
    /// <summary>
    /// Cache management for PlayerDataHandler
    /// </summary>
    public abstract partial class PlayerDataHandler
    {
        #region Static Fields

        // Global cache with player data - single source of truth
        // Using optimized CacheKey struct instead of string concatenation
        private static readonly ConcurrentDictionary<CacheKey, ConcurrentDictionary<string, object?>> _cache = new();

        // Default values for plugins
        private static readonly ConcurrentDictionary<SdkPlugin, ConcurrentDictionary<string, object?>> _defaults = new();

        // All handler instances (for global operations)
        private static readonly ConcurrentDictionary<string, PlayerDataHandler> _handlers = new();

        // Global lock for synchronization
        private static readonly object _globalLock = new();

        // Track if global resources are disposed
        private static bool _disposed = false;

        // Cleanup timer for inactive players
        private static Timer? _cleanupTimer;
        private static readonly object _cleanupLock = new();
        private static DateTime _lastCleanupRun = DateTime.MinValue;

        #endregion

        #region Cache Management

        /// <summary>
        /// Get or create the cache for a player
        /// </summary>
        protected static ConcurrentDictionary<string, object?> GetPlayerCache(ulong steamId, DataType dataType)
        {
            var cacheKey = new CacheKey(steamId, dataType);
            return _cache.GetOrAdd(cacheKey, _ => new ConcurrentDictionary<string, object?>(StringComparer.Ordinal));
        }

        /// <summary>
        /// Build a full cache key with owner plugin prefix
        /// </summary>
        protected string BuildFullKey(string key, string? ownerPlugin = null)
        {
            var actualOwner = ownerPlugin ?? _ownerPlugin;
            return $"{actualOwner}:{key}";
        }

        /// <summary>
        /// Get plugin by owner name
        /// </summary>
        protected static SdkPlugin? GetPluginByOwner(string ownerPlugin)
        {
            foreach (var (plugin, _) in _defaults)
            {
                var pluginFileName = Path.GetFileNameWithoutExtension(plugin.ModulePath);
                if (pluginFileName == ownerPlugin)
                {
                    return plugin;
                }
            }

            return null;
        }

        /// <summary>
        /// Get handler by plugin and data type
        /// </summary>
        public static PlayerDataHandler? GetHandler(SdkPlugin plugin, DataType dataType)
        {
            var pluginName = Path.GetFileNameWithoutExtension(plugin.ModulePath);
            var key = $"{pluginName}:{dataType}";
            return _handlers.TryGetValue(key, out var handler) ? handler : null;
        }

        /// <summary>
        /// Get all registered handlers
        /// </summary>
        public static IEnumerable<PlayerDataHandler> GetAllHandlers() => _handlers.Values;

        /// <summary>
        /// Get the cache for a player statically (for use in Player.Dispose)
        /// </summary>
        public static ConcurrentDictionary<string, object?> GetPlayerCacheStatic(ulong steamId, DataType dataType)
        {
            var cacheKey = new CacheKey(steamId, dataType);
            return _cache.GetOrAdd(cacheKey, _ => new ConcurrentDictionary<string, object?>(StringComparer.Ordinal));
        }

        /// <summary>
        /// Clear data for a specific player
        /// </summary>
        public void ClearPlayerData(ulong steamId)
        {

            // Clear only THIS plugin's data from the shared cache, not the entire cache
            var cache = GetPlayerCache(steamId, _dataType);
            var keysToRemove = cache.Keys.Where(key => key.StartsWith($"{_ownerPlugin}:", StringComparison.Ordinal)).ToList();

            foreach (var key in keysToRemove)
            {
                cache.TryRemove(key, out _);
            }

        }

        /// <summary>
        /// Clear this plugin's data from all player caches
        /// </summary>
        private void ClearPluginDataFromAllCaches()
        {
            var pluginName = _ownerPlugin;
            var keyPrefix = $"{pluginName}:";

            // Remove all entries from shared cache that belong to this plugin
            foreach (var (_, cache) in _cache)
            {
                // Get keys to remove (need to create a list to avoid modification during enumeration)
                var keysToRemove = cache.Keys.Where(k => k.StartsWith(keyPrefix, StringComparison.Ordinal)).ToList();

                // Remove all matching keys
                foreach (var key in keysToRemove)
                {
                    cache.TryRemove(key, out _);
                }
            }
        }

        #endregion

        #region Default Values

        /// <summary>
        /// Register default values for this handler
        /// </summary>
        public void RegisterDefaults(Dictionary<string, object?> defaults)
        {
            var pluginDefaults = _defaults.GetOrAdd(_plugin, _ => new ConcurrentDictionary<string, object?>());

            foreach (var item in defaults)
            {
                pluginDefaults[item.Key] = item.Value;
            }
        }

        /// <summary>
        /// Reset player data to default values
        /// </summary>
        public async Task ResetPlayerDataAsync(ulong steamId)
        {
            try
            {
                // Clear the in-memory cache first
                var cache = GetPlayerCache(steamId, _dataType);

                // Get the default values for this plugin
                if (_defaults.TryGetValue(_plugin, out var defaults))
                {
                    // Reset each key to its default value
                    foreach (var kvp in defaults)
                    {
                        string fullKey = BuildFullKey(kvp.Key, null);
                        cache[fullKey] = kvp.Value;
                    }
                }

                // Update the database
                await SavePlayerDataAsync(steamId);

                _logger.LogInformation("Reset {DataType} for player {SteamId}, plugin: {Plugin}", _dataType, steamId, _ownerPlugin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting {DataType} for player {SteamId}", _dataType, steamId);
                throw;
            }
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Simple unified method to load both Settings and Storage data for a player
        /// </summary>
        public static async Task LoadPlayerDataUnifiedAsync(ulong steamId)
        {
            var handlers = _handlers.Values.ToList();

            foreach (var handler in handlers)
            {
            }

            if (handlers.Count == 0) return;

            try
            {
                // Load all handlers for this player
                var loadTasks = handlers.Select(handler => handler.LoadPlayerDataAsync(steamId));
                await Task.WhenAll(loadTasks);


                foreach (var handler in handlers.OfType<StorageHandler>())
                {
                    handler.StoreOriginalValues(steamId);
                }

                // Don't fire the event here - let the caller decide when to fire it
                // This allows Player constructor to set IsLoaded = true first
                // FireUnifiedPlayerDataLoadEvent(steamId);
            }
            catch (Exception ex)
            {
                var logger = handlers.FirstOrDefault()?._logger;
                logger?.LogError(ex, "Error in unified data loading for player {SteamId}", steamId);
            }
        }

        /// <summary>
        /// Simple bulk loading for multiple players
        /// </summary>
        public static async Task LoadMultiplePlayersUnifiedAsync(IEnumerable<ulong> steamIds)
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0) return;

            var handlers = _handlers.Values.ToList();
            if (handlers.Count == 0) return;

            try
            {
                // Load all data types for all players in parallel
                var loadTasks = handlers.Select(handler => handler.LoadMultiplePlayersAsync(steamIdList));
                await Task.WhenAll(loadTasks);

                // Store original values for storage handlers
                foreach (var handler in handlers.OfType<StorageHandler>())
                {
                    foreach (var steamId in steamIdList)
                    {
                        handler.StoreOriginalValues(steamId);
                    }
                }

                // Fire unified load events only for players that are marked as loaded
                // This method is typically used for bulk loading, so we need to check each player
                foreach (var steamId in steamIdList)
                {
                    var player = Core.Base.Player.Find(steamId);
                    if (player?.IsLoaded == true)
                    {
                        FireUnifiedPlayerDataLoadEvent(steamId);
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = handlers.FirstOrDefault()?._logger;
                logger?.LogError(ex, "Error in unified bulk loading for {Count} players", steamIdList.Count);
            }
        }

        /// <summary>
        /// Fires a unified player data load event across all registered plugins
        /// </summary>
        public static void FireUnifiedPlayerDataLoadEvent(ulong steamId)
        {
            // Group handlers by unique owner plugin
            var uniqueHandlers = _handlers.Values
                .Where(h => h._eventManager != null)
                .GroupBy(h => h._ownerPlugin)
                .Select(g => g.First());

            foreach (var handler in uniqueHandlers)
            {
                var eventArgs = new PlayerDataEventArgs(steamId, handler._ownerPlugin, EventType.PlayerDataLoad);
                handler._eventManager!.Dispatch(eventArgs, HookMode.Post);
            }
        }

        /// <summary>
        /// Initialize cleanup timer
        /// </summary>
        private static void InitializeCleanupTimer()
        {
            lock (_cleanupLock)
            {
                if (_cleanupTimer != null)
                    return;

                // Schedule cleanup to run every 8 hours using System.Threading.Timer
                _cleanupTimer = new Timer(
                    async _ => await RunInactivePlayerCleanup(),
                    null,
                    TimeSpan.FromHours(8), // Initial delay
                    TimeSpan.FromHours(8)  // Repeat interval
                );
            }
        }

        /// <summary>
        /// Run cleanup for inactive players
        /// </summary>
        private static async Task RunInactivePlayerCleanup()
        {
            lock (_cleanupLock)
            {
                if (DateTime.UtcNow - _lastCleanupRun < TimeSpan.FromDays(1))
                    return;

                _lastCleanupRun = DateTime.UtcNow;
            }

            var handlers = _handlers.Values.ToList();
            if (handlers.Count == 0) return;

            // Use the first handler for database operations
            var handler = handlers.First();
            var logger = handler._logger;

            try
            {
                // Get retention days from unified playerdata config
                int retentionDays = SdkInternalConfig.GetValue<int>("inactive_player_retention_days", "playerdata", 30);

                logger.LogInformation("Running inactive player cleanup. Retention: {Days} days", retentionDays);

                // Run cleanup for all handler types with same retention
                foreach (var h in handlers)
                {
                    try
                    {
                        await h.CleanupInactivePlayersAsync(retentionDays);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to cleanup inactive players for {Handler}", h._ownerPlugin);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during inactive player cleanup");
            }
        }

        /// <summary>
        /// Clean up global resources when no handlers remain
        /// </summary>
        private static void CleanupGlobalResources()
        {
            lock (_globalLock)
            {
                if (_disposed)
                    return;

                _disposed = true;

                // Kill cleanup timer
                lock (_cleanupLock)
                {
                    _cleanupTimer?.Dispose();
                    _cleanupTimer = null;
                }

                // Clear all static collections
                _defaults.Clear();
                _handlers.Clear();
                _cache.Clear();
            }
        }

        #endregion
    }
}