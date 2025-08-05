using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Services.Data.Base;
using Kitsune.SDK.Services.Data.Cache.Keys;
using Dapper;
using System.Text.Json;
using System.Dynamic;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Services.Config;

namespace Kitsune.SDK.Services.Data.Storage
{
    /// <summary>
    /// Handles player storage management for Kitsune SDK with type-safe generic access
    /// </summary>
    public sealed class StorageHandler : PlayerDataHandler
    {
        // Function delegate for loading player storage
        public delegate Task LoadPlayerStorageDelegate(ulong steamId);

        // Functions to load storage data for specific plugins
        private static readonly ConcurrentDictionary<SdkPlugin, LoadPlayerStorageDelegate> _loadStorageFunctions = new();

        // Cache for typed storage instances - using optimized key structure
        private static readonly ConcurrentDictionary<InstanceCacheKey, object> _typedStorageInstances = new();

        // Cache for dynamic storage instances - using optimized key structure
        private static readonly ConcurrentDictionary<DynamicCacheKey, object> _dynamicStorageInstances = new();

        // Context registry for storage instances - maps instance to its handler and steamId
        private static readonly ConcurrentDictionary<object, (StorageHandler Handler, ulong SteamId)> _instanceContexts = new();

        // Track which properties need external tracking
        private readonly HashSet<string> _trackedProperties = [];

        // Cache for storage metadata
        private readonly Dictionary<string, (bool IsTracked, Type PropertyType)> _storageMetadata = [];

        // Store original values for tracked properties - maps "steamId:propertyName" to original value
        private readonly ConcurrentDictionary<string, object?> _originalValues = new();

        /// <summary>
        /// Creates a new storage handler for the specified plugin
        /// </summary>
        /// <param name="plugin">The plugin context</param>
        public StorageHandler(SdkPlugin plugin) : base(plugin, DataType.Storage)
        {
            // Register playerdata configs when handler is created (fire and forget)
            _ = SdkInternalConfig.RegisterPlayerDataAsync(plugin);
            
            // Register for player loading
            RegisterForPlayerLoading(plugin, this);
        }

        #region Generic Storage Implementation

        /// <summary>
        /// Gets a typed storage instance for a player
        /// </summary>
        /// <typeparam name="TStorage">The storage type</typeparam>
        /// <param name="steamId">The player's SteamID</param>
        /// <returns>A typed storage instance</returns>
        public TStorage GetTypedStorage<TStorage>(ulong steamId) where TStorage : class, new()
        {
            var key = new InstanceCacheKey(_plugin, typeof(TStorage), steamId);

            // Get existing or create new instance
            return (TStorage)_typedStorageInstances.GetOrAdd(key, _ =>
            {
                var instance = new TStorage();

                // Register context for this instance
                _instanceContexts[instance] = (this, steamId);

                InitializeStorageInstance(instance, typeof(TStorage), steamId);

                // Store original values for tracked properties after initialization
                StoreOriginalValuesForPlayer(steamId);

                return instance;
            });
        }

        /// <summary>
        /// Gets the context (handler and steamId) for a storage instance
        /// </summary>
        internal static (StorageHandler Handler, ulong SteamId)? GetContext(object instance)
            => _instanceContexts.TryGetValue(instance, out var context) ? context : null;

        /// <summary>
        /// Initializes a storage instance by registering its properties and setting up value binding
        /// </summary>
        private void InitializeStorageInstance(object instance, Type storageType, ulong steamId)
        {
            // Get cached metadata for this type
            var metadata = MetadataCache.GetTypeMetadata(storageType);
            bool isStorageBase = instance is StorageBase;

            // Register default values dictionary
            var defaults = new Dictionary<string, object?>(metadata.StorageProperties.Length);

            // Copy tracked properties from metadata
            foreach (var trackedProp in metadata.TrackedProperties)
            {
                _trackedProperties.Add(trackedProp);
            }

            // Process all storage properties from cached metadata
            foreach (var propMeta in metadata.StorageProperties)
            {
                // For StorageBase derived classes, skip initialization of auto-properties
                // as they will use Get/Set methods
                if (isStorageBase && propMeta.IsAutoProperty)
                {
                    // Only register defaults, don't set values directly
                    object? defaultValue = propMeta.Property.CanRead ? propMeta.Property.GetValue(instance) : propMeta.DefaultValue;
                    defaults[propMeta.Name] = defaultValue;

                    // Store metadata about this property
                    _storageMetadata[propMeta.Name] = (propMeta.IsTracked, propMeta.Property.PropertyType);
                }
                else if (!isStorageBase)
                {
                    // Original logic for non-StorageBase classes
                    object? defaultValue = propMeta.Property.GetValue(instance) ?? propMeta.DefaultValue;
                    defaults[propMeta.Name] = defaultValue;
                    _storageMetadata[propMeta.Name] = (propMeta.IsTracked, propMeta.Property.PropertyType);

                    // Load the actual value from cache/handler and set it directly to the property
                    var currentValue = GetStorageValue<object>(steamId, propMeta.Name);
                    if (currentValue != null)
                    {
                        try
                        {
                            var convertedValue = TypeConverter.ConvertDynamic(currentValue, propMeta.Property.PropertyType);
                            if (convertedValue != null)
                            {
                                propMeta.Property.SetValue(instance, convertedValue);
                            }
                            else
                            {
                                propMeta.Property.SetValue(instance, currentValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error setting property {PropertyName} for player {SteamId}: {Value} -> {PropertyType}",
                                propMeta.Property.Name, steamId, currentValue, propMeta.Property.PropertyType.Name);
                        }
                    }
                }
            }

            // Register all defaults at once
            if (defaults.Count > 0)
            {
                Register(defaults);
            }
        }

        /// <summary>
        /// Global provider for typed storage instances
        /// </summary>
        internal static class GenericStorageProvider
        {
            /// <summary>
            /// Gets typed storage for a player
            /// </summary>
            public static TStorage GetTypedStorage<TStorage>(Player player) where TStorage : class, new()
            {
                // Find the storage handler for each plugin that implements ISdkStorage<TStorage>
                var pluginWithHandler = FindPluginImplementingInterface<TStorage>();

                // If specific plugin implementation found, use it
                if (pluginWithHandler != null)
                {
                    var storageHandler = GetHandlerForPlugin(pluginWithHandler)
                        ?? throw new InvalidOperationException($"Storage handler not found for plugin {pluginWithHandler.ModuleName}");

                    return storageHandler.GetTypedStorage<TStorage>(player.SteamID);
                }

                // Otherwise, find the plugin in the call stack (for local storage access)
                if (PlayerHandlerHelpers.FindPluginInCallStack() is not SdkPlugin callingPlugin)
                    throw new InvalidOperationException($"Cannot determine plugin context for Storage<{typeof(TStorage).Name}>");

                var localStorageHandler = GetHandlerForPlugin(callingPlugin)
                    ?? throw new InvalidOperationException($"Storage handler not found for the current plugin");

                return localStorageHandler.GetTypedStorage<TStorage>(player.SteamID);
            }

            /// <summary>
            /// Gets the plugin storage for the specified plugin
            /// </summary>
            public static dynamic GetPluginStorage(Player player, string pluginName)
            {
                // Find the plugin by name
                SdkPlugin? plugin = null;

                foreach (var p in PlayerHandlerHelpers.GetAllPlugins())
                {
                    string pName = Path.GetFileNameWithoutExtension(p.ModulePath);
                    if (pName.Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                    {
                        plugin = p as SdkPlugin;
                        break;
                    }
                }

                if (plugin == null)
                    throw new InvalidOperationException($"Plugin '{pluginName}' not found");

                // Get the storage handler for this plugin
                var storageHandler = GetHandlerForPlugin(plugin)
                    ?? throw new InvalidOperationException($"Storage handler not found for plugin {pluginName}");

                // Create a type-checking dynamic storage proxy that uses TypeConverter for validation
                return DynamicStorageProvider.GetDynamicStorage(player, plugin, storageHandler);
            }

            /// <summary>
            /// Finds the plugin that implements the ISdkStorage interface for a specific storage type
            /// </summary>
            private static SdkPlugin? FindPluginImplementingInterface<TStorage>() where TStorage : class, new()
            {
                Type targetInterface = typeof(ISdkStorage<TStorage>);

                foreach (var plugin in PlayerHandlerHelpers.GetAllPlugins().OfType<SdkPlugin>())
                {
                    if (plugin.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkStorage<>) && i.GetGenericArguments()[0] == typeof(TStorage)))
                    {
                        return plugin;
                    }
                }

                return null;
            }

            /// <summary>
            /// Gets the storage handler for a plugin
            /// </summary>
            private static StorageHandler? GetHandlerForPlugin(SdkPlugin plugin)
            {
                return GetHandler(plugin, DataType.Storage) as StorageHandler;
            }
        }

        /// <summary>
        /// Provider for dynamic storage objects
        /// </summary>
        internal static class DynamicStorageProvider
        {
            /// <summary>
            /// Gets or creates a dynamic storage object for the given player and plugin
            /// </summary>
            public static object GetDynamicStorage(Player player, SdkPlugin plugin, StorageHandler handler)
            {
                var key = new DynamicCacheKey(plugin, player.SteamID);

                // Get existing or create new dynamic proxy
                return _dynamicStorageInstances.GetOrAdd(key, _ => new DynamicStorageProxy(player.SteamID, handler));
            }
        }

        #endregion

        #region Tracking Methods

        /// <summary>
        /// Store original values for tracked properties after loading
        /// </summary>
        internal void StoreOriginalValues(ulong steamId)
        {
            if (_trackedProperties.Count == 0)
                return;

            var cache = GetPlayerCache(steamId, DataType.Storage);
            foreach (var trackedProperty in _trackedProperties)
            {
                var fullKey = BuildFullKey(trackedProperty);
                if (cache.TryGetValue(fullKey, out var value))
                {
                    var originalKey = $"{steamId}:{trackedProperty}";
                    _originalValues[originalKey] = value;
                }
            }
        }

        /// <summary>
        /// Store original values for a specific player (called after typed storage is created)
        /// </summary>
        private void StoreOriginalValuesForPlayer(ulong steamId)
        {
            if (_trackedProperties.Count == 0)
                return;

            var cache = GetPlayerCache(steamId, DataType.Storage);
            foreach (var trackedProperty in _trackedProperties)
            {
                var originalKey = $"{steamId}:{trackedProperty}";

                // Only store if we haven't already stored an original value
                if (!_originalValues.ContainsKey(originalKey))
                {
                    var fullKey = BuildFullKey(trackedProperty);
                    if (cache.TryGetValue(fullKey, out var value))
                    {
                        _originalValues[originalKey] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Synchronize tracked properties from database before applying changes
        /// </summary>
        internal async Task SynchronizeTrackedProperties(ulong steamId)
        {
            if (_trackedProperties.Count == 0 || _isDisposing)
                return;

            try
            {
                using var connection = await CreateConnectionAsync();
                var tableName = TableName;
                var query = $@"
                    SELECT `{StringEx.ValidateSqlIdentifier(_columnName)}`
                    FROM `{StringEx.ValidateSqlIdentifier(tableName)}`
                    WHERE `steam_id` = @SteamID";

                var result = await connection.ExecuteScalarAsync<string>(query, new { SteamID = steamId.ToString() });

                if (result != null)
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(result);
                    if (data != null)
                    {
                        var cache = GetPlayerCache(steamId, _dataType);

                        foreach (var trackedProperty in _trackedProperties)
                        {
                            var originalKey = $"{steamId}:{trackedProperty}";
                            var fullKey = BuildFullKey(trackedProperty);

                            // Get original and current values
                            if (_originalValues.TryGetValue(originalKey, out var originalValue) && cache.TryGetValue(fullKey, out var currentValue) && data.TryGetValue(trackedProperty, out var dbValue))
                            {
                                // Convert dbValue from JsonElement if needed
                                if (dbValue is JsonElement jsonElement && originalValue != null)
                                {
                                    dbValue = TypeConverter.ConvertDynamic(jsonElement, originalValue.GetType());
                                }

                                // Calculate delta
                                var delta = CalculateDelta(originalValue, currentValue);

                                if (delta != null)
                                {
                                    // Apply delta to DB value
                                    var newValue = ApplyDelta(dbValue, delta);
                                    cache[fullKey] = newValue;

                                    // Update original value for next save
                                    _originalValues[originalKey] = newValue;
                                }
                                else
                                {
                                    // Non-numeric or no change - just use current value
                                    cache[fullKey] = currentValue;
                                    _originalValues[originalKey] = currentValue;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing tracked properties for player {SteamId}", steamId);
            }
        }

        /// <summary>
        /// Calculate the difference between original and current value
        /// </summary>
        private static object? CalculateDelta(object? originalValue, object? currentValue)
        {
            if (originalValue == null || currentValue == null)
                return null;

            try
            {
                return (originalValue, currentValue) switch
                {
                    (int a, int b) => b - a,
                    (long a, long b) => b - a,
                    (float a, float b) => b - a,
                    (double a, double b) => b - a,
                    (decimal a, decimal b) => b - a,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Apply delta to a value
        /// </summary>
        private static object? ApplyDelta(object? baseValue, object? delta)
        {
            if (baseValue == null || delta == null)
                return baseValue;

            try
            {
                return (baseValue, delta) switch
                {
                    (int a, int b) => a + b,
                    (long a, long b) => a + b,
                    (float a, float b) => a + b,
                    (double a, double b) => a + b,
                    (decimal a, decimal b) => a + b,
                    _ => baseValue
                };
            }
            catch
            {
                return baseValue;
            }
        }

        /// <summary>
        /// Clear original values when player data is cleared
        /// </summary>
        internal void ClearOriginalValues(ulong steamId)
        {
            foreach (var trackedProperty in _trackedProperties)
            {
                var originalKey = $"{steamId}:{trackedProperty}";
                _originalValues.TryRemove(originalKey, out _);
            }
        }

        #endregion

        #region Registration and Value Access

        /// <summary>
        /// Register default storage values for the module
        /// </summary>
        /// <param name="defaultStorage">Default storage key-value pairs</param>
        /// <exception cref="ArgumentException">Thrown when a storage key has an invalid format</exception>
        public void Register(Dictionary<string, object?> defaultStorage)
        {
            // Validate each key before registering
            foreach (var key in defaultStorage.Keys)
            {
                StringEx.ValidateName(key, nameof(defaultStorage));
            }

            // Register defaults in base class
            RegisterDefaults(defaultStorage);

            // Register each key with the global key registry
            foreach (var key in defaultStorage.Keys)
            {
                Player.RegisterKeySource($"{_ownerPlugin}:{key}", _plugin, true);
            }
        }

        /// <summary>
        /// Get player storage value from memory
        /// </summary>
        public T? GetStorageValue<T>(ulong steamId, string key, string? ownerPlugin = null)
            => GetValue<T>(steamId, key, ownerPlugin);

        /// <summary>
        /// Set player storage value in memory
        /// </summary>
        public void SetStorageValue<T>(ulong steamId, string key, T value, string? ownerPlugin = null, bool saveImmediately = false)
        {
            SetValue(steamId, key, value, ownerPlugin, saveImmediately);
        }

        /// <summary>
        /// Get player storage value - async version for database access
        /// For normal runtime usage, prefer GetStorageValue
        /// </summary>
        public async Task<T?> GetStorageValueAsync<T>(ulong steamId, string key, string? ownerPlugin = null)
        {
            // Try memory cache first
            var memoryValue = GetValue<T>(steamId, key, ownerPlugin);
            if (memoryValue != null)
            {
                return memoryValue;
            }

            try
            {
                using var connection = await CreateConnectionAsync();
                string fullKey = BuildFullKey(key, ownerPlugin);
                string targetOwner = ownerPlugin ?? _ownerPlugin;
                string columnName = $"{targetOwner}.storage";

                var tableName = TableName;
                var query = $@"
                    SELECT `{StringEx.ValidateSqlIdentifier(columnName)}`
                    FROM `{StringEx.ValidateSqlIdentifier(tableName)}`
                    WHERE `steam_id` = @SteamID";

                var result = await connection.ExecuteScalarAsync<string>(query, new { SteamID = steamId.ToString() });

                if (result != null)
                {
                    using var document = JsonDocument.Parse(result);
                    var data = document.RootElement;

                    if (data.TryGetProperty(fullKey, out var element))
                    {
                        try
                        {
                            var value = TypeConverter.Convert<T>(element);
                            if (value != null)
                            {
                                // Update cache with fetched value
                                var cache = GetPlayerCache(steamId, _dataType);
                                cache[fullKey] = value;
                                return value;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error converting value for key '{Key}' to type '{Type}'", key, typeof(T).Name);
                        }
                    }
                }

                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value from DB for player {SteamId}, key {Key}", steamId, key);
                return default;
            }
        }

        /// <summary>
        /// Set player storage value - async version for database operations
        /// For normal runtime usage, prefer SetStorageValue
        /// </summary>
        public async Task SetStorageValueAsync<T>(ulong steamId, string key, T value, string? ownerPlugin = null, bool saveImmediately = false)
        {
            // Update in-memory first
            SetValue(steamId, key, value, ownerPlugin, false);

            // Save immediately if requested
            if (saveImmediately)
            {
                await SavePlayerDataAsync(steamId);
            }
        }

        #endregion

        #region Player Loading

        /// <summary>
        /// Register a plugin for player storage loading
        /// </summary>
        internal static void RegisterForPlayerLoading(SdkPlugin plugin, StorageHandler handler)
        {
            _loadStorageFunctions[plugin] = handler.LoadPlayerDataAsync;
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clean up typed storage instances for a player
        /// </summary>
        internal static void CleanupPlayerInstances(ulong steamId)
        {
            // Remove all typed storage instances for this player
            foreach (var key in _typedStorageInstances.Keys.Where(k => k.SteamId == steamId).ToList())
            {
                if (_typedStorageInstances.TryRemove(key, out var instance))
                {
                    // Also remove from context registry
                    _instanceContexts.TryRemove(instance, out _);
                }
            }

            // Remove all dynamic storage instances for this player
            foreach (var key in _dynamicStorageInstances.Keys.Where(k => k.SteamId == steamId).ToList())
            {
                _dynamicStorageInstances.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clean up instances for a plugin
        /// </summary>
        internal static void CleanupPluginInstances(SdkPlugin plugin)
        {
            // Remove all typed storage instances for this plugin
            foreach (var key in _typedStorageInstances.Keys.Where(k => k.Plugin == plugin).ToList())
            {
                if (_typedStorageInstances.TryRemove(key, out var instance))
                {
                    // Also remove from context registry
                    _instanceContexts.TryRemove(instance, out _);
                }
            }

            // Remove all dynamic storage instances for this plugin
            foreach (var key in _dynamicStorageInstances.Keys.Where(k => k.Plugin == plugin).ToList())
            {
                _dynamicStorageInstances.TryRemove(key, out _);
            }

            // Remove from load functions
            _loadStorageFunctions.TryRemove(plugin, out _);
        }

        /// <summary>
        /// Dispose resources when shutting down
        /// </summary>
        public override void Dispose()
        {
            // Clean up instances for this plugin
            CleanupPluginInstances(_plugin);

            base.Dispose();
        }

        #endregion
    }

    #region Dynamic Storage Proxy

    /// <summary>
    /// Dynamic proxy for storage objects - enables property-like access to storage values
    /// with built-in TypeConverter validation
    /// </summary>
    internal class DynamicStorageProxy(ulong steamId, StorageHandler handler) : DynamicObject
    {
        private readonly ulong _steamId = steamId;
        private readonly StorageHandler _handler = handler;

        // Cache for known property types
        private readonly Dictionary<string, Type> _propertyTypeCache = [];

        // Dynamic property getter
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = _handler.GetStorageValue<object>(_steamId, binder.Name);

            // Cache the type if we have a non-null value
            if (result != null)
            {
                _propertyTypeCache[binder.Name] = result.GetType();
            }

            return true;
        }

        // Dynamic property setter with type checking
        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (value == null)
            {
                _handler.SetStorageValue(_steamId, binder.Name, value);
                return true;
            }

            // Check if we know the expected type for this property
            if (_propertyTypeCache.TryGetValue(binder.Name, out var expectedType) && expectedType != value.GetType())
            {
                // Type mismatch - try to convert using TypeConverter
                var convertedValue = TypeConverter.ConvertDynamic(value, expectedType);
                if (convertedValue != null)
                {
                    _handler.SetStorageValue(_steamId, binder.Name, convertedValue);
                    return true;
                }
            }

            // If no type in cache or type matches, store directly
            // SetStorageValue will handle incremental logic internally
            _handler.SetStorageValue(_steamId, binder.Name, value);

            // Cache the type for future reference
            _propertyTypeCache[binder.Name] = value.GetType();

            return true;
        }

        // Support for dictionary-like access
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
        {
            if (indexes.Length == 1 && indexes[0] is string key)
            {
                result = _handler.GetStorageValue<object>(_steamId, key);

                // Cache the type if we have a non-null value
                if (result != null)
                {
                    _propertyTypeCache[key] = result.GetType();
                }

                return true;
            }

            result = null;
            return false;
        }

        // Support for dictionary-like assignment with type checking
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value)
        {
            if (indexes.Length == 1 && indexes[0] is string key && value != null)
            {
                // Check if we know the expected type for this key
                if (_propertyTypeCache.TryGetValue(key, out var expectedType) && expectedType != value.GetType())
                {
                    // Type mismatch - try to convert using TypeConverter
                    var convertedValue = TypeConverter.ConvertDynamic(value, expectedType);
                    if (convertedValue != null)
                    {
                        _handler.SetStorageValue(_steamId, key, convertedValue);
                        return true;
                    }
                }

                // If no type in cache or type matches, store directly
                _handler.SetStorageValue(_steamId, key, value);

                // Cache the type for future reference
                _propertyTypeCache[key] = value.GetType();

                return true;
            }

            return false;
        }
    }

    #endregion
}