using Kitsune.SDK.Utilities;

namespace Kitsune.SDK.Services.Config
{
    /// <summary>
    /// Extensions for the ConfigHandler class to support global configurations
    /// </summary>
    public static class ConfigHandlerExtensions
    {
        /// <summary>
        /// Gets the value of a global configuration
        /// </summary>
        /// <typeparam name="T">The type to convert the value to</typeparam>
        /// <param name="handler">The config handler</param>
        /// <param name="ownerName">The name of the plugin that owns the config</param>
        /// <param name="configName">The name of the config</param>
        /// <returns>The value of the config, or default if not found</returns>
        public static T? GetGlobalValue<T>(this ConfigHandler handler, string ownerName, string configName)
        {
            var entry = GlobalConfigRegistry.Instance.GetGlobalConfig(ownerName, configName);
            if (entry == null || entry.ConfigItem.Value == null)
                return default;

            // Direct match with requested type
            if (entry.ConfigItem.Value is T typedValue)
                return typedValue;

            // Try conversion using TypeConverter
            try
            {
                return TypeConverter.Convert<T>(entry.ConfigItem.Value);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Sets the value of a global configuration
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="handler">The config handler</param>
        /// <param name="ownerName">The name of the plugin that owns the config</param>
        /// <param name="configName">The name of the config</param>
        /// <param name="value">The new value to set</param>
        /// <returns>True if the update was successful, false otherwise</returns>
        public static bool SetGlobalValue<T>(this ConfigHandler handler, string ownerName, string configName, T value) where T : notnull
        {
            return GlobalConfigRegistry.Instance.UpdateGlobalConfigValue(handler._plugin, ownerName, configName, value);
        }
    }
}