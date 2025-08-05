
using System.Reflection;
using System.Runtime.CompilerServices;
using Kitsune.SDK.Core.Interfaces;
using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Services.Data.Settings;
using Kitsune.SDK.Services.Data.Storage;
using Kitsune.SDK.Services.Data.Base;
using Kitsune.SDK.Core.Attributes.Data;

using SdkPlayer = Kitsune.SDK.Core.Base.Player;
using static Kitsune.SDK.Services.Data.Settings.SettingsHandler;
using static Kitsune.SDK.Services.Data.Storage.StorageHandler;

namespace Kitsune.SDK.Extensions.Player
{
    /// <summary>
    /// Extension methods for Player storage and settings access
    /// </summary>
    public static class PlayerExtensions
    {
        #region Generic Storage Access

        /// <summary>
        /// Gets a typed storage instance for the player.
        /// Use this method for type-safe access to local storage:
        /// <code>
        /// var gameData = player.Storage&lt;PlayerGameData&gt;();
        /// gameData.Kills++;
        /// </code>
        /// </summary>
        /// <typeparam name="TStorage">The type of storage to get</typeparam>
        /// <returns>A typed storage instance for the local or specified plugin</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TStorage Storage<TStorage>(this CCSPlayerController player) where TStorage : class, new()
        {
            var sdkPlayer = SdkPlayer.Find(player);
            if (sdkPlayer != null)
                return GenericStorageProvider.GetTypedStorage<TStorage>(sdkPlayer);

            throw new InvalidOperationException("Player not found in SDK player registry");
        }

        /// <summary>
        /// Gets a dynamic storage instance for a specific plugin.
        /// Use this method to access other plugins' storage:
        /// <code>
        /// dynamic otherStorage = player.Storage("other-plugin-name");
        /// otherStorage.SomeProperty = 123;
        /// </code>
        /// </summary>
        /// <param name="pluginName">The name of the plugin</param>
        /// <returns>A dynamic storage instance for the specified plugin</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static dynamic Storage(this CCSPlayerController player, string pluginName)
        {
            var sdkPlayer = SdkPlayer.Find(player);
            if (sdkPlayer != null)
                return GenericStorageProvider.GetPluginStorage(sdkPlayer, pluginName);

            throw new InvalidOperationException("Player not found in SDK player registry");
        }

        #endregion

        #region Generic Settings Access

        /// <summary>
        /// Gets a typed settings instance for the player
        /// </summary>
        /// <typeparam name="TSettings">The type of settings to get</typeparam>
        /// <returns>A typed settings instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TSettings Settings<TSettings>(this CCSPlayerController player) where TSettings : class, new()
        {
            var sdkPlayer = SdkPlayer.Find(player);
            if (sdkPlayer != null)
                return GenericSettingsProvider.GetTypedSettings<TSettings>(sdkPlayer);

            throw new InvalidOperationException("Player not found in SDK player registry");
        }

        /// <summary>
        /// Gets a settings instance for a specific plugin
        /// </summary>
        /// <param name="pluginName">The name of the plugin</param>
        /// <returns>A settings instance for the specified plugin</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static dynamic Settings(this CCSPlayerController player, string pluginName)
        {
            var sdkPlayer = SdkPlayer.Find(player);
            if (sdkPlayer != null)
                return GenericSettingsProvider.GetPluginSettings(sdkPlayer, pluginName);

            throw new InvalidOperationException("Player not found in SDK player registry");
        }

        #endregion

        #region Reset Methods

        /// <summary>
        /// Resets storage data to default values for a specific type.
        /// This method will find all properties with [Storage] attributes and reset them to their default values.
        /// </summary>
        /// <typeparam name="TStorage">The type of storage to reset</typeparam>
        /// <returns>A task representing the async reset operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task ResetStorage<TStorage>(this CCSPlayerController player) where TStorage : class, new()
        {
            // Get the storage instance
            var storage = player.Storage<TStorage>();

            // Create a new instance to get default values
            var defaultInstance = new TStorage();

            // Use reflection to reset all properties with [Storage] attribute
            var properties = typeof(TStorage).GetProperties()
                .Where(p => p.GetCustomAttribute<StorageAttribute>() != null && p.CanWrite);

            foreach (var property in properties)
            {
                var defaultValue = property.GetValue(defaultInstance);
                property.SetValue(storage, defaultValue);
            }

            // Save the reset data
            var handler = GetStorageHandler<TStorage>();
            if (handler != null)
            {
                await handler.SavePlayerDataAsync(player.SteamID);
            }
        }

        /// <summary>
        /// Resets settings data to default values for a specific type.
        /// This method will find all properties with [Setting] attributes and reset them to their default values.
        /// </summary>
        /// <typeparam name="TSettings">The type of settings to reset</typeparam>
        /// <returns>A task representing the async reset operation</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task ResetSettings<TSettings>(this CCSPlayerController player) where TSettings : class, new()
        {
            // Get the settings instance
            var settings = player.Settings<TSettings>();

            // Create a new instance to get default values
            var defaultInstance = new TSettings();

            // Use reflection to reset all properties with [Setting] attribute
            var properties = typeof(TSettings).GetProperties()
                .Where(p => p.GetCustomAttribute<SettingAttribute>() != null && p.CanWrite);

            foreach (var property in properties)
            {
                var defaultValue = property.GetValue(defaultInstance);
                property.SetValue(settings, defaultValue);
            }

            // Save the reset data
            var handler = GetSettingsHandler<TSettings>();
            if (handler != null)
            {
                await handler.SavePlayerSettingsAsync(player.SteamID);
            }
        }

        /// <summary>
        /// Gets the storage handler for a specific storage type
        /// </summary>
        private static StorageHandler? GetStorageHandler<TStorage>() where TStorage : class, new()
        {
            // Find the plugin that implements ISdkStorage<TStorage>
            var plugin = PlayerHandlerHelpers.GetAllPlugins()
                .OfType<SdkPlugin>()
                .FirstOrDefault(p => p.GetType().GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(ISdkStorage<>) &&
                    i.GetGenericArguments()[0] == typeof(TStorage)));

            // Try to find from call stack
            plugin ??= PlayerHandlerHelpers.FindPluginInCallStack() as SdkPlugin;

            return plugin != null ? PlayerDataHandler.GetHandler(plugin, PlayerDataHandler.DataType.Storage) as StorageHandler : null;
        }

        /// <summary>
        /// Gets the settings handler for a specific settings type
        /// </summary>
        private static SettingsHandler? GetSettingsHandler<TSettings>() where TSettings : class, new()
        {
            // Find the plugin that implements ISdkSettings<TSettings>
            var plugin = PlayerHandlerHelpers.GetAllPlugins()
                .OfType<SdkPlugin>()
                .FirstOrDefault(p => p.GetType().GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(ISdkSettings<>) &&
                    i.GetGenericArguments()[0] == typeof(TSettings)));

            // Try to find from call stack
            plugin ??= PlayerHandlerHelpers.FindPluginInCallStack() as SdkPlugin;

            return plugin != null ? PlayerDataHandler.GetHandler(plugin, PlayerDataHandler.DataType.Settings) as SettingsHandler : null;
        }

        #endregion

        #region Player (SDK) Extensions

        /// <summary>
        /// Extension methods for SDK Player class
        /// </summary>
        public static TStorage Storage<TStorage>(this SdkPlayer player) where TStorage : class, new()
        {
            return GenericStorageProvider.GetTypedStorage<TStorage>(player);
        }

        public static dynamic Storage(this SdkPlayer player, string pluginName)
        {
            return GenericStorageProvider.GetPluginStorage(player, pluginName);
        }

        public static TSettings Settings<TSettings>(this SdkPlayer player) where TSettings : class, new()
        {
            return GenericSettingsProvider.GetTypedSettings<TSettings>(player);
        }

        public static dynamic Settings(this SdkPlayer player, string pluginName)
        {
            return GenericSettingsProvider.GetPluginSettings(player, pluginName);
        }

        public static async Task ResetStorage<TStorage>(this SdkPlayer player) where TStorage : class, new()
        {
            // Get the storage instance
            var storage = player.Storage<TStorage>();

            // Create a new instance to get default values
            var defaultInstance = new TStorage();

            // Use reflection to reset all properties with [Storage] attribute
            var properties = typeof(TStorage).GetProperties()
                .Where(p => p.GetCustomAttribute<StorageAttribute>() != null && p.CanWrite);

            foreach (var property in properties)
            {
                var defaultValue = property.GetValue(defaultInstance);
                property.SetValue(storage, defaultValue);
            }

            // Save the reset data
            var handler = GetStorageHandler<TStorage>();
            if (handler != null)
            {
                await handler.SavePlayerDataAsync(player.SteamID);
            }
        }

        public static async Task ResetSettings<TSettings>(this SdkPlayer player) where TSettings : class, new()
        {
            // Get the settings instance
            var settings = player.Settings<TSettings>();

            // Create a new instance to get default values
            var defaultInstance = new TSettings();

            // Use reflection to reset all properties with [Setting] attribute
            var properties = typeof(TSettings).GetProperties()
                .Where(p => p.GetCustomAttribute<SettingAttribute>() != null && p.CanWrite);

            foreach (var property in properties)
            {
                var defaultValue = property.GetValue(defaultInstance);
                property.SetValue(settings, defaultValue);
            }

            // Save the reset data
            var handler = GetSettingsHandler<TSettings>();
            if (handler != null)
            {
                await handler.SavePlayerSettingsAsync(player.SteamID);
            }
        }

        #endregion
    }
}