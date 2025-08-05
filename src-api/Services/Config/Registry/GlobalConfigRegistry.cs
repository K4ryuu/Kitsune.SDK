using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Models.Config;
using Kitsune.SDK.Core.Base;

namespace Kitsune.SDK.Services.Config
{
    /// <summary>
    /// Central registry for global configuration items that can be shared across plugins
    /// </summary>
    public class GlobalConfigRegistry
    {
        // Singleton instance
        private static readonly Lazy<GlobalConfigRegistry> _instance = new(() => new GlobalConfigRegistry());

        // Public accessor for singleton
        public static GlobalConfigRegistry Instance => _instance.Value;

        // Dictionary of all global configs, keyed by their name (pluginName.configName)
        private readonly ConcurrentDictionary<string, GlobalConfigEntry> _globalConfigs = new();

        private GlobalConfigRegistry() { }

        /// <summary>
        /// Registers a configuration item as global, making it accessible by other plugins
        /// </summary>
        /// <param name="plugin">The plugin that owns this config</param>
        /// <param name="configItem">The config item to register</param>
        /// <param name="groupName">The group name for this config</param>
        /// <returns>True if registration was successful, false if the config was already registered</returns>
        internal bool RegisterGlobalConfig(SdkPlugin plugin, ConfigItem configItem, string groupName)
        {
            // Use the DLL filename instead of ModuleName for more consistent identification
            string dllName = Path.GetFileNameWithoutExtension(plugin.ModulePath);
            string globalKey = GetGlobalConfigKey(dllName, configItem.Name);

            var entry = new GlobalConfigEntry
            {
                OwnerPlugin = plugin,
                ConfigItem = configItem,
                GroupName = groupName,
                OwnerName = dllName
            };

            return _globalConfigs.TryAdd(globalKey, entry);
        }

        /// <summary>
        /// Updates the value of a global configuration
        /// </summary>
        /// <param name="plugin">The plugin requesting the update</param>
        /// <param name="ownerName">The name of the plugin that owns the config</param>
        /// <param name="configName">The name of the config</param>
        /// <param name="newValue">The new value to set</param>
        /// <returns>True if update was successful, false otherwise</returns>
        internal bool UpdateGlobalConfigValue(SdkPlugin plugin, string ownerName, string configName, object newValue)
        {
            string globalKey = GetGlobalConfigKey(ownerName, configName);

            if (!_globalConfigs.TryGetValue(globalKey, out var entry))
                return false;

            // Check if the plugin has access to modify this config
            if (entry.OwnerPlugin != plugin && entry.ConfigItem.Flags.HasFlag(ConfigFlag.Protected))
            {
                plugin.Logger.LogWarning($"Config '{entry.ConfigItem.Name}' is protected and cannot be modified by {plugin.ModuleName}");
                return false;
            }

            // Check if the config is locked
            if (entry.ConfigItem.Flags.HasFlag(ConfigFlag.Locked))
            {
                plugin.Logger.LogWarning($"Config '{entry.ConfigItem.Name}' is locked and cannot be modified at runtime");
                return false;
            }

            // Update the value
            entry.ConfigItem.Value = newValue;

            // Update owner plugin's config instances
            var configHandler = entry.OwnerPlugin.Config;
            configHandler?.UpdateConfigInstancesWithNewValue(entry.ConfigItem.Name, newValue);

            return true;
        }

        /// <summary>
        /// Gets a global configuration entry
        /// </summary>
        /// <param name="ownerName">The name of the plugin that owns the config (DLL name)</param>
        /// <param name="configName">The name of the config</param>
        /// <returns>The global config entry if found, null otherwise</returns>
        internal GlobalConfigEntry? GetGlobalConfig(string ownerName, string configName)
        {
            string globalKey = GetGlobalConfigKey(ownerName, configName);

            if (_globalConfigs.TryGetValue(globalKey, out var entry))
                return entry;

            return null;
        }

        /// <summary>
        /// Unregisters all global configs for a plugin
        /// </summary>
        /// <param name="plugin">The plugin to unregister</param>
        internal void UnregisterPlugin(SdkPlugin plugin)
        {
            // Remove all global configs owned by this plugin
            foreach (var entry in _globalConfigs.Where(kv => kv.Value.OwnerPlugin == plugin).ToList())
            {
                _globalConfigs.TryRemove(entry.Key, out _);
            }
        }

        /// <summary>
        /// Gets a unique key for a global config
        /// </summary>
        private static string GetGlobalConfigKey(string pluginName, string configName)
        {
            return $"{pluginName.ToLowerInvariant()}.{configName.ToLowerInvariant()}";
        }
    }

    /// <summary>
    /// Represents a global configuration entry in the registry
    /// </summary>
    public class GlobalConfigEntry
    {
        public required SdkPlugin OwnerPlugin { get; set; }
        public required ConfigItem ConfigItem { get; set; }
        public required string GroupName { get; set; }
        public required string OwnerName { get; set; }
    }
}