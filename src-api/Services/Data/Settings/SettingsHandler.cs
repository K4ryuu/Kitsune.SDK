using System.Collections.Concurrent;
using System.Diagnostics;
using System.Dynamic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Services.Data.Base;
using Kitsune.SDK.Services.Data.Cache.Keys;
using Dapper;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Services.Config;

namespace Kitsune.SDK.Services.Data.Settings
{
    /// <summary>
    /// Handles player settings management for Kitsune SDK with type-safe generic access
    /// </summary>
    public sealed class SettingsHandler : PlayerDataHandler
    {
        // Function delegate for loading player settings
        public delegate Task LoadPlayerSettingsDelegate(ulong steamId);

        // Functions to load settings data for specific plugins
        private static readonly ConcurrentDictionary<SdkPlugin, LoadPlayerSettingsDelegate> _loadSettingsFunctions = new();

        // Cache for typed settings instances - using optimized key structure
        private static readonly ConcurrentDictionary<InstanceCacheKey, object> _typedSettingsInstances = new();

        // Cache for dynamic settings instances - using optimized key structure
        private static readonly ConcurrentDictionary<DynamicCacheKey, object> _dynamicSettingsInstances = new();

        // Context registry for settings instances - maps instance to its handler and steamId
        private static readonly ConcurrentDictionary<object, (SettingsHandler Handler, ulong SteamId)> _instanceContexts = new();

        /// <summary>
        /// Creates a new settings handler for the specified plugin
        /// </summary>
        /// <param name="plugin">The plugin context</param>
        public SettingsHandler(SdkPlugin plugin) : base(plugin, DataType.Settings)
        {
            // Register playerdata configs when handler is created (fire and forget)
            _ = SdkInternalConfig.RegisterPlayerDataAsync(plugin);
            
            // Register for player loading
            RegisterForPlayerLoading(plugin, this);
        }

        #region Generic Settings Implementation

        /// <summary>
        /// Gets a typed settings instance for a player
        /// </summary>
        /// <typeparam name="TSettings">The settings type</typeparam>
        /// <param name="steamId">The player's SteamID</param>
        /// <returns>A typed settings instance</returns>
        public TSettings GetTypedSettings<TSettings>(ulong steamId) where TSettings : class, new()
        {
            var key = new InstanceCacheKey(_plugin, typeof(TSettings), steamId);

            // Get existing or create new instance
            return (TSettings)_typedSettingsInstances.GetOrAdd(key, _ =>
            {
                var instance = new TSettings();

                // Register context for this instance
                _instanceContexts[instance] = (this, steamId);

                InitializeSettingsInstance(instance, typeof(TSettings), steamId);
                return instance;
            });
        }

        /// <summary>
        /// Gets the context (handler and steamId) for a settings instance
        /// </summary>
        internal static (SettingsHandler Handler, ulong SteamId)? GetContext(object instance)
            => _instanceContexts.TryGetValue(instance, out var context) ? context : null;

        /// <summary>
        /// Initializes a settings instance by registering its properties and setting up value binding
        /// </summary>
        private void InitializeSettingsInstance(object instance, Type settingsType, ulong steamId)
        {
            // Get cached metadata for this type
            var metadata = MetadataCache.GetTypeMetadata(settingsType);
            bool isSettingsBase = instance is SettingsBase;

            // Register default values dictionary
            var defaults = new Dictionary<string, object?>(metadata.SettingsProperties.Length);

            // Process all settings properties from cached metadata
            foreach (var propMeta in metadata.SettingsProperties)
            {
                // For SettingsBase derived classes, skip initialization of auto-properties
                // as they will use Get/Set methods
                if (isSettingsBase && propMeta.IsAutoProperty)
                {
                    // Only register defaults, don't set values directly
                    object? defaultValue = propMeta.Property.CanRead ? propMeta.Property.GetValue(instance) : propMeta.DefaultValue;
                    defaults[propMeta.Name] = defaultValue;
                }
                else if (!isSettingsBase)
                {
                    // Original logic for non-SettingsBase classes
                    object? defaultValue = propMeta.Property.GetValue(instance) ?? propMeta.DefaultValue;
                    defaults[propMeta.Name] = defaultValue;

                    // Load the actual value from cache/handler and set it directly to the property
                    var currentValue = GetSettingValue<object>(steamId, propMeta.Name);
                    if (currentValue != null)
                    {
                        try
                        {
                            // Convert and set the actual property value
                            var convertedValue = TypeConverter.ConvertDynamic(currentValue, propMeta.Property.PropertyType);
                            if (convertedValue != null)
                            {
                                propMeta.Property.SetValue(instance, convertedValue);
                            }
                            else
                            {
                                // If conversion failed, try direct assignment
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
        /// Global provider for typed settings instances
        /// </summary>
        internal static class GenericSettingsProvider
        {
            /// <summary>
            /// Gets typed settings for a player
            /// </summary>
            public static TSettings GetTypedSettings<TSettings>(Player player) where TSettings : class, new()
            {
                // Find the settings handler for each plugin that implements ISdkSettings<TSettings>
                var pluginWithHandler = FindPluginImplementingInterface<TSettings>()
                    ?? throw new InvalidOperationException($"No plugin implements ISdkSettings<{typeof(TSettings).Name}>");

                var settingsHandler = GetHandlerForPlugin(pluginWithHandler)
                    ?? throw new InvalidOperationException($"Settings handler not found for plugin {pluginWithHandler.ModuleName}");

                return settingsHandler.GetTypedSettings<TSettings>(player.SteamID);
            }

            /// <summary>
            /// Gets the plugin settings for the specified plugin
            /// </summary>
            public static object GetPluginSettings(Player player, string pluginName)
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

                // Get the settings handler for this plugin
                var settingsHandler = GetHandlerForPlugin(plugin)
                    ?? throw new InvalidOperationException($"Settings handler not found for plugin {pluginName}");

                return DynamicSettingsProvider.GetDynamicSettings(player, plugin, settingsHandler);
            }

            /// <summary>
            /// Finds the plugin that implements the ISdkSettings interface for a specific settings type
            /// </summary>
            private static SdkPlugin? FindPluginImplementingInterface<TSettings>() where TSettings : class, new()
            {
                Type targetInterface = typeof(ISdkSettings<TSettings>);

                foreach (var plugin in PlayerHandlerHelpers.GetAllPlugins().OfType<SdkPlugin>())
                {
                    if (plugin.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkSettings<>) && i.GetGenericArguments()[0] == typeof(TSettings)))
                    {
                        return plugin;
                    }
                }

                return null;
            }

            /// <summary>
            /// Gets the settings handler for a plugin
            /// </summary>
            private static SettingsHandler? GetHandlerForPlugin(SdkPlugin plugin)
                => GetHandler(plugin, DataType.Settings) as SettingsHandler;
        }

        /// <summary>
        /// Provider for dynamic settings objects (legacy API)
        /// </summary>
        internal static class DynamicSettingsProvider
        {
            /// <summary>
            /// Gets a dynamic settings object for a player
            /// </summary>
            public static object GetDynamicSettings(Player player)
            {
                // Since this is a static method used by multiple plugins via a shared library,
                // we can't reliably determine which plugin is calling us without some caller info.
                // Instead, we'll use the call stack to find a plugin reference or require an explicit plugin reference.

                // First option: Try to find a SdkPlugin in the call stack
                var stackFrames = new StackTrace().GetFrames();
                if (stackFrames != null)
                {
                    foreach (var frame in stackFrames)
                    {
                        var method = frame.GetMethod();
                        if (method?.DeclaringType?.IsSubclassOf(typeof(SdkPlugin)) == true)
                        {
                            // Found a SdkPlugin in the call stack
                            if (method.DeclaringType.FullName != null && PlayerHandlerHelpers.GetAllPlugins().FirstOrDefault(p => p.GetType().FullName == method.DeclaringType.FullName) is SdkPlugin sdkPlugin)
                            {
                                if (GetHandler(sdkPlugin, DataType.Settings) is SettingsHandler settingsHandler)
                                {
                                    return GetDynamicSettings(player, sdkPlugin, settingsHandler);
                                }
                            }
                        }
                    }
                }

                // Second option: Fall back to the first available plugin with a settings handler
                foreach (var plugin in PlayerHandlerHelpers.GetAllPlugins().OfType<SdkPlugin>())
                {
                    if (GetHandler(plugin, DataType.Settings) is SettingsHandler settingsHandler)
                    {
                        return GetDynamicSettings(player, plugin, settingsHandler);
                    }
                }

                throw new InvalidOperationException("Cannot find a suitable settings handler. Please use player.Settings(\"plugin-name\") for explicit access.");
            }

            /// <summary>
            /// Gets or creates a dynamic settings object for the given player and plugin
            /// </summary>
            public static object GetDynamicSettings(Player player, SdkPlugin plugin, SettingsHandler handler)
            {
                var key = new DynamicCacheKey(plugin, player.SteamID);

                // Get existing or create new dynamic proxy
                return _dynamicSettingsInstances.GetOrAdd(key, _ => new DynamicSettingsProxy(player.SteamID, handler));
            }
        }

        #endregion

        #region Registration and Value Access

        /// <summary>
        /// Register default settings values for the module
        /// </summary>
        /// <param name="defaultSettings">Default settings key-value pairs</param>
        /// <exception cref="ArgumentException">Thrown when a setting name is invalid</exception>
        public void Register(Dictionary<string, object?> defaultSettings)
        {
            // Validate each key name
            foreach (var key in defaultSettings.Keys)
            {
                StringEx.ValidateName(key, nameof(defaultSettings));
            }

            // Register defaults in base class
            RegisterDefaults(defaultSettings);

            // Register each key with the global key registry
            foreach (var key in defaultSettings.Keys)
            {
                Player.RegisterKeySource($"{_ownerPlugin}:{key}", _plugin, false);
            }
        }

        /// <summary>
        /// Get player setting value from memory
        /// </summary>
        public T? GetSettingValue<T>(ulong steamId, string key, string? ownerPlugin = null)
            => GetValue<T>(steamId, key, ownerPlugin);

        /// <summary>
        /// Set player setting value in memory
        /// </summary>
        public void SetSettingValue<T>(ulong steamId, string key, T value, string? ownerPlugin = null, bool saveImmediately = false)
            => SetValue(steamId, key, value, ownerPlugin, saveImmediately);

        /// <summary>
        /// Get player setting value - async version for database access
        /// For normal runtime usage, prefer GetSettingValue
        /// </summary>
        public async Task<T?> GetSettingValueAsync<T>(ulong steamId, string key, string? ownerPlugin = null)
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
                string columnName = $"{targetOwner}.settings";

                var tableName = TableName; // Use the property to get lazy-loaded value
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
        /// Set player setting value - async version for database operations
        /// For normal runtime usage, prefer SetSettingValue
        /// </summary>
        public async Task SetSettingValueAsync<T>(ulong steamId, string key, T value, string? ownerPlugin = null, bool saveImmediately = false)
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
        /// Register a plugin for player settings loading
        /// </summary>
        internal static void RegisterForPlayerLoading(SdkPlugin plugin, SettingsHandler handler)
        {
            _loadSettingsFunctions[plugin] = handler.LoadPlayerDataAsync;
        }

        /// <summary>
        /// Save settings data for a specific player immediately
        /// </summary>
        public Task SavePlayerSettingsAsync(ulong steamId)
            => SavePlayerDataAsync(steamId);

        #endregion

        #region Cleanup

        /// <summary>
        /// Clean up typed settings instances for a player
        /// </summary>
        internal static void CleanupPlayerInstances(ulong steamId)
        {
            // Remove all typed settings instances for this player
            foreach (var key in _typedSettingsInstances.Keys.Where(k => k.SteamId == steamId).ToList())
            {
                if (_typedSettingsInstances.TryRemove(key, out var instance))
                {
                    // Also remove from context registry
                    _instanceContexts.TryRemove(instance, out _);
                }
            }

            // Remove all dynamic settings instances for this player
            foreach (var key in _dynamicSettingsInstances.Keys.Where(k => k.SteamId == steamId).ToList())
            {
                _dynamicSettingsInstances.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clean up instances for a plugin
        /// </summary>
        internal static void CleanupPluginInstances(SdkPlugin plugin)
        {
            // Remove all typed settings instances for this plugin
            foreach (var key in _typedSettingsInstances.Keys.Where(k => k.Plugin == plugin).ToList())
            {
                if (_typedSettingsInstances.TryRemove(key, out var instance))
                {
                    // Also remove from context registry
                    _instanceContexts.TryRemove(instance, out _);
                }
            }

            // Remove all dynamic settings instances for this plugin
            foreach (var key in _dynamicSettingsInstances.Keys.Where(k => k.Plugin == plugin).ToList())
            {
                _dynamicSettingsInstances.TryRemove(key, out _);
            }

            // Remove from load functions
            _loadSettingsFunctions.TryRemove(plugin, out _);
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

    #region Dynamic Settings Proxy

    /// <summary>
    /// Dynamic proxy for settings objects - enables property-like access to settings values
    /// </summary>
    internal class DynamicSettingsProxy(ulong steamId, SettingsHandler handler) : DynamicObject
    {
        private readonly ulong _steamId = steamId;
        private readonly SettingsHandler _handler = handler;

        // Dynamic property getter
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = _handler.GetSettingValue<object>(_steamId, binder.Name);
            return true;
        }

        // Dynamic property setter
        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (value != null)
                _handler.SetSettingValue(_steamId, binder.Name, value);

            return true;
        }

        // Support for dictionary-like access
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
        {
            if (indexes.Length == 1 && indexes[0] is string key)
            {
                result = _handler.GetSettingValue<object>(_steamId, key);
                return true;
            }

            result = null;
            return false;
        }

        // Support for dictionary-like assignment
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value)
        {
            if (indexes.Length == 1 && indexes[0] is string key && value != null)
            {
                _handler.SetSettingValue(_steamId, key, value);
                return true;
            }

            return false;
        }
    }

    #endregion
}