using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Services.Config;

namespace GlobalConfigConsumer
{
    /// <summary>
    /// Example plugin demonstrating consuming global configuration from another plugin
    /// </summary>
    [MinimumApiVersion(300)]
    [MinimumSdkVersion(1)]
    public class GlobalConfigConsumerPlugin : SdkPlugin
    {
        public override string ModuleName => "Global Config Consumer";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
        public override string ModuleDescription => "Example plugin demonstrating consuming global configuration from another plugin";

        protected override void SdkLoad(bool hotReload)
        {
            // Register commands
            Commands.Register("globalinfo", "View global configurations from other plugins", CommandCallback_GlobalInfo);
            Commands.Register("setglobal", "Set global configuration values from other plugins", CommandCallback_SetGlobal);

            // Access global configs directly
            var roundTime = Config.GetGlobalValue<float>("TypeSafeExample", "round_time");
            var gravity = Config.GetGlobalValue<int>("TypeSafeExample", "server_gravity");
        }

        private void CommandCallback_GlobalInfo(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid)
                return;

            player.PrintToChat($" {ChatColors.Green}Global Configs from other plugins:");

            // Read values directly using Config.GetGlobalValue
            var roundTime = Config.GetGlobalValue<float>("TypeSafeExample", "round_time");
            var gravity = Config.GetGlobalValue<int>("TypeSafeExample", "server_gravity");

            player.PrintToChat($" {ChatColors.Blue}Round Time: {ChatColors.White}{roundTime} minutes");
            player.PrintToChat($" {ChatColors.Blue}Server Gravity: {ChatColors.White}{gravity}");

            // Access other global configs as well
            var welcomeMessage = Config.GetGlobalValue<string>("TypeSafeExample", "welcome_message");
            if (welcomeMessage != null)
            {
                player.PrintToChat($" {ChatColors.Blue}Welcome Message: {ChatColors.White}{welcomeMessage}");
            }
        }

        private void CommandCallback_SetGlobal(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !player.IsValid)
                return;

            if (command.ArgCount < 3)
            {
                player.PrintToChat($" {ChatColors.Red}Usage: /setglobal <config> <value>");
                return;
            }

            string configName = command.ArgByIndex(1);
            string valueStr = command.ArgByIndex(2);

            // Try to modify round time
            if (configName.Equals("roundtime", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(valueStr, out float value))
                {
                    // Attempt to update using Config.SetGlobalValue
                    if (Config.SetGlobalValue("TypeSafeExample", "round_time", value))
                    {
                        player.PrintToChat($" {ChatColors.Green}Updated round time to {value} minutes");
                    }
                    else
                    {
                        player.PrintToChat($" {ChatColors.Red}Failed to update round time (no access)");
                    }
                }
            }
            // Try to modify gravity (will fail due to protected flag)
            else if (configName.Equals("gravity", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(valueStr, out int value))
                {
                    // This will fail because the gravity config is protected
                    if (Config.SetGlobalValue("TypeSafeExample", "server_gravity", value))
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
}