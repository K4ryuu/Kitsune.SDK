using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Utilities;

namespace Kitsune.SDK.Services.Data.Base
{
    /// <summary>
    /// Base class for handling player data (settings and storage)
    /// </summary>
    public abstract partial class PlayerDataHandler : IDisposable
    {
        #region Type Definitions

        /// <summary>
        /// Type of data this handler manages
        /// </summary>
        public enum DataType
        {
            Settings,
            Storage
        }

        #endregion

        #region Instance Fields

        // Core components
        protected readonly ILogger _logger;
        protected readonly SdkPlugin _plugin;
        protected readonly ISdkEventManager? _eventManager;
        protected readonly DataType _dataType;

        // Configuration fields
        protected readonly string _columnName;
        protected readonly string _ownerPlugin;
        protected string? _tableName;

        // Track if instance is being disposed
        protected bool _isDisposing = false;

        // Last save times to throttle saves
        private readonly ConcurrentDictionary<ulong, DateTime> _lastSaveTime = new();

        #endregion

        #region Properties

        // Public properties for batch operations
        public string OwnerPlugin => _ownerPlugin;
        public DataType HandlerDataType => _dataType;

        /// <summary>
        /// Gets the plugin that owns this handler
        /// </summary>
        public SdkPlugin Plugin => _plugin;

        /// <summary>
        /// Gets the table name (lazy-loaded)
        /// </summary>
        public string TableName => GetTableName();

        #endregion

        #region Constructor

        /// <summary>
        /// Base constructor
        /// </summary>
        protected PlayerDataHandler(SdkPlugin plugin, DataType dataType)
        {
            _plugin = plugin;
            _logger = plugin.Logger;
            _eventManager = plugin.Events;
            _dataType = dataType;
            _ownerPlugin = Path.GetFileNameWithoutExtension(plugin.ModulePath);
            _columnName = $"{_ownerPlugin}.{_dataType.ToString().ToLowerInvariant()}";

            // Register this handler globally
            var handlerKey = $"{_ownerPlugin}:{_dataType}";
            _handlers[handlerKey] = this;

            // Initialize cleanup timer on first handler
            InitializeCleanupTimer();
        }

        #endregion

        #region Value Access Methods

        /// <summary>
        /// Get a value from cache
        /// </summary>
        public T? GetValue<T>(ulong steamId, string key, string? ownerPlugin = null)
        {
            try
            {
                var fullKey = BuildFullKey(key, ownerPlugin);
                var cache = GetPlayerCache(steamId, _dataType);

                // Try to get from cache first
                if (cache.TryGetValue(fullKey, out var value))
                {
                    // Direct match with requested type
                    if (value is T typedValue)
                        return typedValue;

                    // Handle null
                    if (value == null)
                        return default;

                    // Try using TypeConverter
                    try
                    {
                        var converted = TypeConverter.Convert<T>(value);
                        if (converted != null)
                        {
                            // Update cache with converted value
                            cache[fullKey] = converted;
                            return converted;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Value conversion failed for key '{Key}': {Error}", key, ex.Message);
                    }

                    // Special JsonElement handling
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        try
                        {
                            var converted = TypeConverter.Convert<T>(jsonElement);
                            if (converted != null)
                            {
                                cache[fullKey] = converted;
                                return converted;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("JsonElement conversion failed for key '{Key}': {Error}", key, ex.Message);
                        }
                    }

                    _logger.LogWarning("Cannot convert {SourceType} to {TargetType} for key '{Key}'",
                        value.GetType().Name, typeof(T).Name, key);
                    return default;
                }

                // Try default values
                var actualOwner = ownerPlugin ?? _ownerPlugin;
                var targetPlugin = GetPluginByOwner(actualOwner);

                if (targetPlugin != null && _defaults.TryGetValue(targetPlugin, out var defaults) &&
                    defaults.TryGetValue(key, out var defaultValue))
                {
                    // Direct match
                    if (defaultValue is T typedDefault)
                        return typedDefault;

                    // Handle null
                    if (defaultValue == null)
                        return default;

                    // Try conversion
                    try
                    {
                        return TypeConverter.Convert<T>(defaultValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Default value conversion failed for key '{Key}': {Error}", key, ex.Message);
                    }
                }

                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value for player {SteamId}, key {Key}", steamId, key);
                return default;
            }
        }

        /// <summary>
        /// Set a value in memory and optionally save
        /// </summary>
        public void SetValue<T>(ulong steamId, string key, T value, string? ownerPlugin = null, bool saveImmediately = false)
        {
            // Security check for cross-plugin access
            if (ownerPlugin != null && ownerPlugin != _ownerPlugin)
            {
                _logger.LogWarning("Plugin '{OurPlugin}' is trying to set data for plugin '{TargetPlugin}'. Access denied.",
                    _ownerPlugin, ownerPlugin);
                return;
            }

            string fullKey = BuildFullKey(key, ownerPlugin);

            // Update cache
            var cache = GetPlayerCache(steamId, _dataType);
            cache[fullKey] = value;

            if (saveImmediately)
            {
                _ = SavePlayerDataAsync(steamId);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Update last save time
        /// </summary>
        private void UpdateSaveTime(ulong steamId)
        {
            _lastSaveTime[steamId] = DateTime.UtcNow;
        }

        /// <summary>
        /// Check if player can be saved (throttle control)
        /// </summary>
        private bool CanSavePlayer(ulong steamId)
        {
            if (!_lastSaveTime.TryGetValue(steamId, out var lastSave))
                return true;

            return (DateTime.UtcNow - lastSave).TotalSeconds >= 1;
        }

        /// <summary>
        /// Fire an SDK event
        /// </summary>
        protected bool FireEvent(ulong steamId, EventType eventType, HookMode hookMode)
        {
            if (_eventManager == null)
                return true;

            var eventArgs = new PlayerDataEventArgs(steamId, _ownerPlugin, eventType);
            return _eventManager.Dispatch(eventArgs, hookMode);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Clean up resources
        /// </summary>
        public virtual void Dispose()
        {
            // Set disposing flag first to prevent new operations
            _isDisposing = true;

            lock (_globalLock)
            {
                // Remove from global handlers
                var handlerKey = $"{_ownerPlugin}:{_dataType}";
                _handlers.TryRemove(handlerKey, out _);

                // Clear this instance's collections
                _defaults.TryRemove(_plugin, out _);

                // Clear this plugin's data from all player caches
                ClearPluginDataFromAllCaches();

                // If no handlers remain, clean up global resources
                if (_handlers.IsEmpty)
                {
                    CleanupGlobalResources();

                    // Reset the disposed flag after cleanup to allow hot reload
                    _disposed = false;
                }
            }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}