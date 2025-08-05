using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Core.Base;

using SdkPlayer = Kitsune.SDK.Core.Base.Player;

namespace Kitsune.SDK.Extensions.Player
{
    /// <summary>
    /// Provides typed access to player storage and settings based on plugin interfaces
    /// </summary>
    public static class TypedPlayerExtensions
    {
        // Cache for plugin -> storage type mappings
        private static readonly ConcurrentDictionary<Type, Type> _pluginStorageTypes = new();

        // Cache for plugin -> settings type mappings
        private static readonly ConcurrentDictionary<Type, Type> _pluginSettingsTypes = new();

        /// <summary>
        /// Gets the storage type for a plugin
        /// </summary>
        private static Type GetPluginStorageType(SdkPlugin plugin)
        {
            return _pluginStorageTypes.GetOrAdd(plugin.GetType(), type =>
            {
                // Find the ISdkStorage<T> interface
                var storageInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkStorage<>))
                        ?? throw new InvalidOperationException($"Plugin {plugin.ModuleName} does not implement ISdkStorage<>");

                // Return the type argument of ISdkStorage<T>
                return storageInterface.GetGenericArguments()[0];
            });
        }

        /// <summary>
        /// Gets the settings type for a plugin
        /// </summary>
        private static Type GetPluginSettingsType(SdkPlugin plugin)
        {
            return _pluginSettingsTypes.GetOrAdd(plugin.GetType(), type =>
            {
                // Find the ISdkSettings<T> interface
                var settingsInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkSettings<>))
                        ?? throw new InvalidOperationException($"Plugin {plugin.ModuleName} does not implement ISdkSettings<>");

                // Return the type argument of ISdkSettings<T>
                return settingsInterface.GetGenericArguments()[0];
            });
        }

        /// <summary>
        /// Gets a typed player to provide strongly typed access to player data
        /// </summary>
        /// <typeparam name="TStorage">The storage type</typeparam>
        /// <typeparam name="TSettings">The settings type</typeparam>
        /// <returns>A typed player instance</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypedPlayer<TStorage, TSettings> AsTyped<TStorage, TSettings>(this SdkPlayer player) where TStorage : class, new() where TSettings : class, new()
        {
            return new TypedPlayer<TStorage, TSettings>(player);
        }

        /// <summary>
        /// Creates a typed player for the current plugin context
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static dynamic AsTypedForPlugin(this SdkPlayer player, SdkPlugin plugin)
        {
            // Get storage and settings types
            Type storageType = GetPluginStorageType(plugin);
            Type settingsType = GetPluginSettingsType(plugin);

            // Create generic TypedPlayer<TStorage, TSettings> using reflection
            Type typedPlayerType = typeof(TypedPlayer<,>).MakeGenericType(storageType, settingsType);

            // Create and return a new instance
            return Activator.CreateInstance(typedPlayerType, player)
                ?? throw new InvalidOperationException($"Failed to create instance of {typedPlayerType}");
        }
    }

    /// <summary>
    /// Provides strongly typed access to player data
    /// </summary>
    /// <typeparam name="TStorage">The storage type used by the plugin</typeparam>
    /// <typeparam name="TSettings">The settings type used by the plugin</typeparam>
    public class TypedPlayer<TStorage, TSettings> where TStorage : class, new() where TSettings : class, new()
    {
        private readonly SdkPlayer _player;

        internal TypedPlayer(SdkPlayer player)
        {
            _player = player;
        }

        /// <summary>
        /// The player instance
        /// </summary>
        public SdkPlayer Player => _player;

        /// <summary>
        /// Local storage with strong typing
        /// </summary>
        public TStorage Storage => _player.Storage<TStorage>();

        /// <summary>
        /// Local settings with strong typing
        /// </summary>
        public TSettings Settings => _player.Settings<TSettings>();

        /// <summary>
        /// Resets storage data to default values
        /// </summary>
        /// <returns>A task representing the async reset operation</returns>
        public Task ResetStorageAsync() => _player.ResetStorage<TStorage>();

        /// <summary>
        /// Resets settings data to default values
        /// </summary>
        /// <returns>A task representing the async reset operation</returns>
        public Task ResetSettingsAsync() => _player.ResetSettings<TSettings>();
    }
}