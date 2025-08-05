using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Config;
using Kitsune.SDK.Core.Attributes.Data;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Core.Models.Config;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Services.Config;

namespace TypeSafeExample
{
    /// <summary>
    /// Example plugin demonstrating the type-safe, interface-based API
    /// </summary>
    ///
    [MinimumApiVersion(300)]
    [MinimumSdkVersion(1)]
    public class TypeSafeExamplePlugin : SdkPlugin,
        ISdkConfig<ServerConfig>,          // Type-safe configuration
        ISdkStorage<PlayerGameData>,       // Type-safe storage
        ISdkSettings<PlayerPreferences>    // Type-safe settings
    {
        public override string ModuleName => "Type-Safe Example";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
        public override string ModuleDescription => "Example plugin demonstrating the type-safe, interface-based API";

        // ISdkConfig<ServerConfig> implementation
        public new ServerConfig Config => GetTypedConfig<ServerConfig>();

        protected override void SdkLoad(bool hotReload)
        {
            // Register commands
            Commands.Register("gamedata", "View your game data", CommandCallback_GameData);
            Commands.Register("preferences", "View your preferences", CommandCallback_Preferences);
            Commands.Register("setcolor", "Set your HUD color", CommandCallback_SetColor);
            Commands.Register("globalconfig", "Global config examples", CommandCallback_GlobalConfig);
        }

        private void CommandCallback_GlobalConfig(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid)
                return;

            if (command.ArgCount < 3)
            {
                player.PrintToChat($" {ChatColors.Red}Usage: /globalconfig <get|set> <value>");
                return;
            }

            string action = command.ArgByIndex(1);

            // Read a global config value
            if (action.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                var roundTime = base.Config.GetGlobalValue<float>("TypeSafeExample", "round_time");
                var gravity = base.Config.GetGlobalValue<int>("TypeSafeExample", "server_gravity");

                player.PrintToChat($" {ChatColors.Green}Global Configs:");
                player.PrintToChat($" {ChatColors.Blue}Round Time: {ChatColors.White}{roundTime} minutes");
                player.PrintToChat($" {ChatColors.Blue}Server Gravity: {ChatColors.White}{gravity}");
            }
            // Set a global config value
            else if (action.Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                string configName = command.ArgByIndex(2);
                string valueStr = command.ArgByIndex(3);

                if (configName.Equals("roundtime", StringComparison.OrdinalIgnoreCase))
                {
                    if (float.TryParse(valueStr, out float value))
                    {
                        if (base.Config.SetGlobalValue("TypeSafeExample", "round_time", value))
                        {
                            player.PrintToChat($" {ChatColors.Green}Updated round time to {value} minutes");
                        }
                        else
                        {
                            player.PrintToChat($" {ChatColors.Red}Failed to update round time");
                        }
                    }
                }
                else if (configName.Equals("gravity", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(valueStr, out int value))
                    {
                        if (base.Config.SetGlobalValue("TypeSafeExample", "server_gravity", value))
                        {
                            player.PrintToChat($" {ChatColors.Green}Updated server gravity to {value}");
                        }
                        else
                        {
                            player.PrintToChat($" {ChatColors.Red}Failed to update server gravity (protected)");
                        }
                    }
                }
            }
        }

        private void CommandCallback_GameData(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid)
                return;

            // Get the player wrapper
            var kitsunePlayer = Player.GetOrCreate<Player>(player);
            if (kitsunePlayer == null)
                return;

            // Access external plugin storage with dynamic typing
            dynamic externalStorage = kitsunePlayer.Storage("third-party-plugin-name");
            externalStorage.Kills = 10; // Example of setting a value in external storage

            // Access local storage with type safety
            // This provides compile-time type checking and IDE IntelliSense
            var gameData = kitsunePlayer.Storage<PlayerGameData>();
            gameData.Kills++;

            // Display the data to the player
            player.PrintToChat($" {ChatColors.Green}Your game data:");
            player.PrintToChat($" {ChatColors.Blue}Kills: {ChatColors.White}{gameData.Kills}");
            player.PrintToChat($" {ChatColors.Blue}Deaths: {ChatColors.White}{gameData.Deaths}");

            // Increment visits count
            gameData.Visits++;
            player.PrintToChat($" {ChatColors.Blue}Visits: {ChatColors.White}{gameData.Visits}");
        }

        private void CommandCallback_Preferences(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid)
                return;

            // Get the player wrapper
            var kitsunePlayer = Player.GetOrCreate<Player>(player);
            if (kitsunePlayer == null)
                return;

            // Access settings directly through the property

            // Access settings with type safety
            var preferences = kitsunePlayer.Settings<PlayerPreferences>();

            // Display the preferences to the player
            player.PrintToChat($" {ChatColors.Green}Your preferences:");
            player.PrintToChat($" {ChatColors.Blue}HUD Color: {ChatColors.White}{preferences.HudColor}");
            player.PrintToChat($" {ChatColors.Blue}Notifications: {ChatColors.White}{(preferences.NotificationsEnabled ? "Enabled" : "Disabled")}");
        }

        private void CommandCallback_SetColor(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid)
                return;

            if (command.ArgCount < 2)
            {
                player.PrintToChat($" {ChatColors.Red}Usage: /setcolor <color>");
                return;
            }

            string color = command.ArgByIndex(1);

            // Get the player wrapper
            var kitsunePlayer = Player.GetOrCreate<Player>(player);
            if (kitsunePlayer == null)
                return;

            // Use the type-safe settings API
            var preferences = kitsunePlayer.Settings<PlayerPreferences>();
            preferences.HudColor = color;

            player.PrintToChat($" {ChatColors.Green}HUD color set to: {ChatColors.White}{color}");
        }
    }

    /// <summary>
    /// Server configuration class with type-safe properties
    /// </summary>
    public class ServerConfig
    {
        [Config("spawn_protection", "Duration of spawn protection in seconds")]
        [ConfigValidation(nameof(ValidateProtectionTime))]
        public float SpawnProtectionTime { get; set; } = 3.0f;

        [Config("welcome_message", "Message to show players when they join", ConfigFlag.Global)]
        public string WelcomeMessage { get; set; } = "Welcome to the server!";

        [Config("max_score", "Maximum score a player can achieve")]
        public int MaxScore { get; set; } = 1000;

        [Config("round_time", "Round time in minutes (shared globally)", ConfigFlag.Global)]
        public float RoundTime { get; set; } = 2.0f;

        [Config("server_gravity", "Server gravity setting (globally readable, but protected)", ConfigFlag.Global | ConfigFlag.Protected)]
        public int ServerGravity { get; set; } = 800;

        // Validation method - must be private/public, returning bool and taking one parameter of the property type
        private bool ValidateProtectionTime(float value)
        {
            // Spawn protection must be between 0 and 10 seconds
            return value >= 0 && value <= 10;
        }
    }

    /// <summary>
    /// Player game data - stored in the database
    /// </summary>
    public class PlayerGameData
    {
        [Storage("kills", "Player's kill count")]
        public int Kills { get; set; } = 0;

        [Storage("deaths", "Player's death count")]
        public int Deaths { get; set; } = 0;

        [Storage("visits", "Number of times the player has viewed their stats")]
        public int Visits { get; set; } = 0;
    }

    /// <summary>
    /// Player preferences - stored in the database
    /// </summary>
    public class PlayerPreferences
    {
        [Setting("hud_color", "Player's HUD color preference")]
        public string HudColor { get; set; } = "default";

        [Setting("notifications", "Whether the player wants to receive notifications")]
        public bool NotificationsEnabled { get; set; } = true;
    }
}