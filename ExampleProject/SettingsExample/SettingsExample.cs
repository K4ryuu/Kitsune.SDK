using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes;
using Kitsune.SDK.Core.Attributes.Data;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Utilities;

namespace Kitsune.Examples
{
	/// <summary>
	/// Example showing settings handler functionality with type-safe API
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class SettingsExample : SdkPlugin, ISdkSettings<PlayerSettings>
	{
		public override string ModuleName => "Kitsune Settings Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Example demonstrating type-safe settings handler usage";

		protected override void SdkLoad(bool hotReload)
		{
			// Settings are automatically registered via the PlayerSettings class attributes
			// No need for manual registration anymore!

			// Register commands to demonstrate settings
			RegisterCommands();

			// Register event handlers
			RegisterEventHandler<EventPlayerActivate>(OnPlayerConnect);
			RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
		}

		private void RegisterCommands()
		{
			// Command to view all settings
			Commands.Register("settings", "View your settings", HandleSettingsCommand);

			// Command to change a specific setting
			Commands.Register("set", "Change a setting value", HandleSetCommand, argCount: 2, helpText: "<setting> <value>");

			// Command to toggle boolean settings
			Commands.Register("toggle", "Toggle a boolean setting", HandleToggleCommand, argCount: 1, helpText: "<setting>");

			// Commands to reset settings to defaults
			Commands.Register("reset", "Reset your settings to defaults", HandleResetCommand);
			Commands.Register("resetplayersettings", "Reset another player's settings", HandleResetPlayerSettingsCommand, argCount: 1, helpText: "<player>", permission: "@kitsune/admin", usage: CommandUsage.CLIENT_AND_SERVER);

			// Command to demonstrate type-safe access
			Commands.Register("typesafe", "Show type-safe settings access", HandleTypeSafeCommand);
		}

		private void HandleSettingsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			// Get typed settings object
			var settings = player.Settings<PlayerSettings>();

			info.ReplyToCommand($" {ChatColors.Gold}=== Your Settings ===");
			info.ReplyToCommand($" {ChatColors.Green}Language: {settings.Language}");
			info.ReplyToCommand($" {ChatColors.Green}Volume: {settings.Volume:F1}");
			info.ReplyToCommand($" {ChatColors.Green}Notifications: {settings.Notifications}");
			info.ReplyToCommand($" {ChatColors.Green}Theme: {settings.Theme}");
			info.ReplyToCommand($" {ChatColors.Green}Show FPS: {settings.ShowFps}");
		}

		private void HandleSetCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			string settingName = info.GetArg(1).ToLower();
			string value = info.GetArg(2);

			// Get typed settings object
			var settings = player.Settings<PlayerSettings>();

			try
			{
				switch (settingName)
				{
					case "language":
						settings.Language = value;
						info.ReplyToCommand($" {ChatColors.Green}Set language to: {value}");
						break;

					case "volume":
						if (float.TryParse(value, out float floatValue))
						{
							settings.Volume = Math.Clamp(floatValue, 0f, 1f);
							info.ReplyToCommand($" {ChatColors.Green}Set volume to: {settings.Volume:F1}");
						}
						else
						{
							info.ReplyToCommand($" {ChatColors.Red}Invalid float value: {value}");
						}
						break;

					case "notifications":
						if (bool.TryParse(value, out bool boolValue))
						{
							settings.Notifications = boolValue;
							info.ReplyToCommand($" {ChatColors.Green}Set notifications to: {boolValue}");
						}
						else
						{
							info.ReplyToCommand($" {ChatColors.Red}Invalid boolean value: {value}");
						}
						break;

					case "theme":
						settings.Theme = value;
						info.ReplyToCommand($" {ChatColors.Green}Set theme to: {value}");
						break;

					case "showfps":
						if (bool.TryParse(value, out bool fpsValue))
						{
							settings.ShowFps = fpsValue;
							info.ReplyToCommand($" {ChatColors.Green}Set show FPS to: {fpsValue}");
						}
						else
						{
							info.ReplyToCommand($" {ChatColors.Red}Invalid boolean value: {value}");
						}
						break;

					default:
						info.ReplyToCommand($" {ChatColors.Red}Unknown setting: {settingName}");
						break;
				}
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Error: {ex.Message}");
			}
		}

		private void HandleToggleCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			string settingName = info.GetArg(1).ToLower();

			// Get typed settings object
			var settings = player.Settings<PlayerSettings>();

			try
			{
				switch (settingName)
				{
					case "notifications":
						settings.Notifications = !settings.Notifications;
						info.ReplyToCommand($" {ChatColors.Green}Toggled notifications to: {settings.Notifications}");
						break;

					case "showfps":
						settings.ShowFps = !settings.ShowFps;
						info.ReplyToCommand($" {ChatColors.Green}Toggled show FPS to: {settings.ShowFps}");
						break;

					default:
						info.ReplyToCommand($" {ChatColors.Red}{settingName} is not a boolean setting");
						break;
				}
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Error: {ex.Message}");
			}
		}

		private void HandleResetCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			try
			{
				CSSThread.RunOnMainThread(async () =>
				{
					// Reset the player's settings to defaults
					await player.ResetSettings<PlayerSettings>();

					info.ReplyToCommand($" {ChatColors.Green}Reset all settings to defaults!");
					info.ReplyToCommand($" {ChatColors.Yellow}Default values: Language: en | Volume: 0.8 | Notifications: true");
				});
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Error resetting settings: {ex.Message}");
			}
		}

		private void HandleResetPlayerSettingsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null && info.CallingContext != CommandCallingContext.Console) return;

			CCSPlayerController? target = info.GetArgTargetResult(1).FirstOrDefault();

			if (target == null || !target.IsValid)
			{
				info.ReplyToCommand($" {ChatColors.Red}Player '{info.GetArg(1)}' not found");
				return;
			}

			var player = Player.Find(target);
			if (player == null) return;

			try
			{
				CSSThread.RunOnMainThread(async () =>
				{
					// Use the new reset functionality for another player
					await player.ResetSettings<PlayerSettings>();

					info.ReplyToCommand($" {ChatColors.Green}Reset settings for player: {target.PlayerName}");
					info.ReplyToCommand($" {ChatColors.Yellow}Their settings have been restored to default values");

					// Notify the target player if they're different from the executor
					if (controller != null && controller != target)
					{
						target.PrintToChat($" {ChatColors.Yellow}An admin has reset your settings to default values!");
					}
				});
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Error resetting player settings: {ex.Message}");
			}
		}

		private void HandleTypeSafeCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			// Demonstrate type-safe access
			var settings = player.Settings<PlayerSettings>();

			info.ReplyToCommand($" {ChatColors.Gold}=== Type-Safe Settings Demo ===");
			info.ReplyToCommand($" {ChatColors.Blue}Direct property access:");
			info.ReplyToCommand($"  Language: {settings.Language} (string)");
			info.ReplyToCommand($"  Volume: {settings.Volume} (float)");
			info.ReplyToCommand($"  Notifications: {settings.Notifications} (bool)");

			// Demonstrate calculations with type safety
			var volumePercentage = (int)(settings.Volume * 100);
			info.ReplyToCommand($" {ChatColors.Blue}Calculated volume: {volumePercentage}%");

			// Demonstrate conditional logic
			if (settings.Notifications && settings.Volume > 0.5f)
			{
				info.ReplyToCommand($" {ChatColors.Green}You have notifications on with audible volume!");
			}
		}

		private HookResult OnPlayerConnect(EventPlayerActivate @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot)
				return HookResult.Continue;

			// Create or get the player instance - this automatically loads their settings
			var sdkPlayer = Player.GetOrCreate<Player>(player);

			// Example: Welcome message based on their language setting
			if (sdkPlayer != null)
			{
				var settings = sdkPlayer.Settings<PlayerSettings>();
				var welcomeMessage = settings.Language switch
				{
					"en" => "Welcome to the server!",
					"es" => "¡Bienvenido al servidor!",
					"fr" => "Bienvenue sur le serveur!",
					"de" => "Willkommen auf dem Server!",
					_ => "Welcome!"
				};

				player.PrintToChat($" {ChatColors.Gold}{welcomeMessage}");
			}

			return HookResult.Continue;
		}

		private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot)
				return HookResult.Continue;

			// Dispose the player - this automatically saves their settings
			Player.Find(player)?.Dispose();

			return HookResult.Continue;
		}

		protected override void SdkUnload(bool hotReload)
		{
			// Resource cleanup is automatically handled by SdkPlugin
		}
	}

	/// <summary>
	/// Type-safe settings class for players
	/// </summary>
	public class PlayerSettings
	{
		[Setting("language", "Player's preferred language")]
		public string Language { get; set; } = "en";

		[Setting("volume", "Player's volume preference (0-1)")]
		public float Volume { get; set; } = 0.8f;

		[Setting("notifications", "Whether to show notifications")]
		public bool Notifications { get; set; } = true;

		[Setting("theme", "UI theme preference")]
		public string Theme { get; set; } = "default";

		[Setting("show_fps", "Whether to show FPS counter")]
		public bool ShowFps { get; set; } = false;
	}
}