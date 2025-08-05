using System.Reflection;
using Kitsune.SDK.Core.Base;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Utilities;

namespace Kitsune.SDK.Services.Config
{
    /// <summary>
    /// Internal SDK configuration manager for SDK-wide settings
    /// </summary>
    internal static class SdkInternalConfig
    {
        private static ConfigHandler? _configHandler;
        private static readonly object _lock = new();
        private static readonly HashSet<string> _registeredComponents = new();

        /// <summary>
        /// Initialize SDK internal configuration with direct path calculation
        /// </summary>
        private static void Initialize(SdkPlugin plugin)
        {
            lock (_lock)
            {
                if (_configHandler != null)
                    return;

                // Create config handler with direct SDK path override
                string sdkDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                _configHandler = new ConfigHandler(plugin, "Kitsune.SDK", sdkDirectory);
            }
        }

        /// <summary>
        /// Register component-specific configurations
        /// </summary>
        public static async Task RegisterComponentAsync(SdkPlugin plugin, string componentName, Action<ConfigHandler> registerAction)
        {
            ConfigHandler? handler;
            lock (_lock)
            {
                // Initialize if needed
                Initialize(plugin);

                // Skip if component already registered
                if (_registeredComponents.Contains(componentName))
                    return;

                handler = _configHandler;
                if (handler == null)
                    return;

                // Register component configs
                registerAction(handler);
                _registeredComponents.Add(componentName);
            }

            // Load config for this component (outside lock to avoid deadlock)
            try
            {
                await handler.LoadGroupConfig(componentName);

                await handler.SaveGroupConfig(componentName, handler._plugin);
            }
            catch (Exception ex)
            {
                // Log any errors
                var logger = handler._plugin?.Logger;
                logger?.LogError(ex, $"SdkInternalConfig: Failed to save config for component: {componentName}");
            }
        }

        /// <summary>
        /// Register ChatProcessor configurations
        /// </summary>
        public static async Task RegisterChatProcessorAsync(SdkPlugin plugin)
        {
            await RegisterComponentAsync(plugin, "chatprocessor", handler =>
            {
                handler.Register("chatprocessor", "enforce_interval", "Interval to enforce chat settings (seconds)", 3.0f);
                handler.Register("chatprocessor", "default_clan_tag", "Default clan tag for players", "{country_short} |");
                handler.Register("chatprocessor", "default_chat_tag", "Default chat tag for players", "{gold}[{country_short}] ");
            });
        }

        /// <summary>
        /// Register PlayerData configurations (both storage and settings)
        /// </summary>
        public static async Task RegisterPlayerDataAsync(SdkPlugin plugin)
        {
            await RegisterComponentAsync(plugin, "playerdata", handler =>
            {
                handler.Register("playerdata", "inactive_player_retention_days", "Days to keep inactive player data", 30);
                handler.Register("playerdata", "save_on_round_end", "Save all player data on round end", true);
            });
        }

        /// <summary>
        /// Register GeoIP placeholders globally
        /// </summary>
        public static void RegisterGeoIP(SdkPlugin plugin)
        {
            lock (_lock)
            {
                // Initialize if needed
                Initialize(plugin);

                // Skip if already registered
                if (_registeredComponents.Contains("geoip"))
                    return;

                // Register GeoIP placeholders
                GeoIP.Initialize(plugin);
                _registeredComponents.Add("geoip");
            }
        }

        /// <summary>
        /// Get a configuration value with safe fallback to default
        /// </summary>
        public static T GetValue<T>(string key, string group = "general", T? defaultValue = default)
        {
            if (_configHandler == null)
            {
                if (defaultValue != null)
                    return defaultValue;

                // Return appropriate default for the type
                if (typeof(T) == typeof(string))
                    return (T)(object)string.Empty;

                return default!;
            }

            try
            {
                var value = _configHandler.GetLocalValue<T>(key, group);
                if (value == null)
                {
                    if (defaultValue != null)
                        return defaultValue;

                    // Return appropriate default for the type
                    if (typeof(T) == typeof(string))
                        return (T)(object)string.Empty;

                    return default(T)!;
                }

                // Special handling for strings - never return null
                if (typeof(T) == typeof(string) && string.IsNullOrEmpty(value as string))
                {
                    if (defaultValue != null && !string.IsNullOrEmpty(defaultValue as string))
                        return defaultValue;
                    return (T)(object)string.Empty;
                }

                return value;
            }
            catch
            {
                if (defaultValue != null)
                    return defaultValue;

                // Return appropriate default for the type
                if (typeof(T) == typeof(string))
                    return (T)(object)string.Empty;

                return default!;
            }
        }
    }
}