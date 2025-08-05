using Kitsune.SDK.Extensions.Player;

using SdkPlayer = Kitsune.SDK.Core.Base.Player;

namespace Kitsune.SDK.Extensions.Plugin
{
    /// <summary>
    /// Extension methods for SDK plugin interfaces
    /// Provides helper methods for working with storage and settings types
    /// </summary>
    public static class SdkPluginExtensions
    {
        /// <summary>
        /// Helper method to get storage for a player
        /// </summary>
        public static TStorage GetPlayerStorage<TStorage>(ulong steamId) where TStorage : class, new()
        {
            var player = SdkPlayer.Find(steamId);
            return player?.Storage<TStorage>() ?? new TStorage();
        }

        /// <summary>
        /// Helper method to get settings for a player
        /// </summary>
        public static TSettings GetPlayerSettings<TSettings>(ulong steamId) where TSettings : class, new()
        {
            var player = SdkPlayer.Find(steamId);
            return player?.Settings<TSettings>() ?? new TSettings();
        }
    }
}