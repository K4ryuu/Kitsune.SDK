using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Player;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Utilities;
using Microsoft.Extensions.Logging;

namespace Kitsune.Examples
{
	/// <summary>
	/// Comprehensive example showing ChatProcessor functionality and how the SDK manages chat processing
	///
	/// ChatProcessor Overview:
	/// - Automatically manages multiple plugins trying to modify chat
	/// - Uses priority system to determine which plugin should handle chat modifications
	/// - Enforces mute/gag status and updates clan tags with placeholders
	/// - Provides formatted chat with dead tags, team tags, custom name tags, colors, etc.
	/// - Handles automatic injection/uninjection when plugins load/unload
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class ChatProcessorExample : SdkPlugin
	{
		public override string ModuleName => "Kitsune ChatProcessor Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Comprehensive example demonstrating ChatProcessor usage and management";

		protected override void SdkLoad(bool hotReload)
		{
			// ChatProcessor automatically injects this plugin when SdkLoad is called
			// The SDK handles the injection process through ChatProcessor.TryInject()
			// Multiple plugins can be registered, but only one is "active" at a time
			ChatProcessor.TryInject(this);

			RegisterCommands();
			RegisterPlaceholders();

			Logger.LogInformation("ChatProcessor example loaded. This plugin is now registered with ChatProcessor.");
			Logger.LogInformation($"Active plugin: {ChatProcessor.GetActivePlugin()?.ModuleName ?? "None"}");
			Logger.LogInformation($"Registered plugins: {ChatProcessor.GetRegisteredPlugins().Count}");
		}

		private void RegisterCommands()
		{
			// Commands to demonstrate ChatProcessor functionality
			Commands.Register("chat-status", "Show ChatProcessor status", HandleChatStatusCommand);
			Commands.Register("chat-plugins", "List registered ChatProcessor plugins", HandleChatPluginsCommand);

			// Chat modification commands
			Commands.Register("set-nametag", "Set custom name tag", HandleSetNameTagCommand, argCount: 1, helpText: "<tag>");
			Commands.Register("set-namecolor", "Set name color", HandleSetNameColorCommand, argCount: 1, helpText: "<color>");
			Commands.Register("set-chatcolor", "Set chat color", HandleSetChatColorCommand, argCount: 1, helpText: "<color>");
			Commands.Register("set-clantag", "Set clan tag", HandleSetClanTagCommand, argCount: 1, helpText: "<tag>");

			// Mute/Gag commands
			Commands.Register("gag", "Gag a player (block text chat)", HandleGagCommand, argCount: 1, helpText: "<player>");
			Commands.Register("ungag", "Ungag a player", HandleUngagCommand, argCount: 1, helpText: "<player>");
			Commands.Register("mute", "Mute a player (block voice chat)", HandleMuteCommand, argCount: 1, helpText: "<player>");
			Commands.Register("unmute", "Unmute a player", HandleUnmuteCommand, argCount: 1, helpText: "<player>");

			// Reset commands
			Commands.Register("reset-chat", "Reset all chat modifications", HandleResetChatCommand);
			Commands.Register("reset-colors", "Reset chat colors", HandleResetColorsCommand);

			// Toggle commands
			Commands.Register("toggle-chatmod", "Toggle chat modifier processing", HandleToggleChatModCommand);
			Commands.Register("colors", "Show available colors", HandleColorsCommand);
		}

		private void RegisterPlaceholders()
		{
			// Register some placeholders that can be used in clan tags
			Placeholders.RegisterPlayer("{kills}", player =>
			{
				var stats = player.ActionTrackingServices?.MatchStats;
				return stats?.Kills.ToString() ?? "0";
			});

			Placeholders.RegisterPlayer("{deaths}", player =>
			{
				var stats = player.ActionTrackingServices?.MatchStats;
				return stats?.Deaths.ToString() ?? "0";
			});

			Placeholders.RegisterPlayer("{score}", player =>
			{
				return player.Score.ToString();
			});

			Placeholders.RegisterServer("{round}", () =>
			{
				// This would need game rule access in a real implementation
				return "1";
			});
		}

		private void HandleChatStatusCommand(CCSPlayerController? controller, CommandInfo info)
		{
			var activePlugin = ChatProcessor.GetActivePlugin();
			var registeredPlugins = ChatProcessor.GetRegisteredPlugins();

			info.ReplyToCommand($" {ChatColors.Gold}=== ChatProcessor Status ===");
			info.ReplyToCommand($" {ChatColors.Green}Active Plugin: {ChatColors.White}{activePlugin?.ModuleName ?? "None"}");
			info.ReplyToCommand($" {ChatColors.Green}Registered Plugins: {ChatColors.White}{registeredPlugins.Count}");

			if (controller != null && controller.IsValid)
			{
				var player = Player.Find(controller);
				if (player != null)
				{
					info.ReplyToCommand($" {ChatColors.Blue}Your Chat Status:");
					info.ReplyToCommand($" {ChatColors.White}  Name Tag: '{player.GetNameTag()}'");
					info.ReplyToCommand($" {ChatColors.White}  Clan Tag: '{player.GetClanTag()}'");
					info.ReplyToCommand($" {ChatColors.White}  Name Color: {player.GetNameColor()}Example{ChatColors.Default}");
					info.ReplyToCommand($" {ChatColors.White}  Chat Color: {player.GetChatColor()}Example{ChatColors.Default}");
					info.ReplyToCommand($" {ChatColors.White}  Gagged: {(player.IsGagged ? ChatColors.Red + "Yes" : ChatColors.Green + "No")}");
					info.ReplyToCommand($" {ChatColors.White}  Muted: {(player.IsMuted ? ChatColors.Red + "Yes" : ChatColors.Green + "No")}");
					info.ReplyToCommand($" {ChatColors.White}  Chat Modifiers: {(player.EnableChatModifiers ? ChatColors.Green + "Enabled" : ChatColors.Red + "Disabled")}");
				}
			}
		}

		private void HandleChatPluginsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			var activePlugin = ChatProcessor.GetActivePlugin();
			var registeredPlugins = ChatProcessor.GetRegisteredPlugins();

			info.ReplyToCommand($" {ChatColors.Gold}=== Registered ChatProcessor Plugins ===");

			for (int i = 0; i < registeredPlugins.Count; i++)
			{
				var plugin = registeredPlugins[i];
				var isActive = plugin == activePlugin;
				var statusColor = isActive ? ChatColors.Green : ChatColors.White;
				var statusText = isActive ? " (ACTIVE)" : "";

				info.ReplyToCommand($" {ChatColors.Blue}{i + 1}. {statusColor}{plugin.ModuleName}{statusText}");
				info.ReplyToCommand($" {ChatColors.Grey}    Version: {plugin.ModuleVersion}");
			}

			if (registeredPlugins.Count == 0)
			{
				info.ReplyToCommand($" {ChatColors.Red}No plugins registered with ChatProcessor");
			}
		}

		private void HandleSetNameTagCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			string tag = info.GetArg(1);

			// Support color codes in the tag
			tag = ChatColor.ReplaceColors(tag, controller);

			player.SetNameTag(tag, ActionPriority.Normal);
			info.ReplyToCommand($" {ChatColors.Green}Name tag set to: '{tag}'");
			info.ReplyToCommand($" {ChatColors.Blue}Tip: Use color codes like {{red}}, {{blue}}, {{team}}, etc.");
		}

		private void HandleSetNameColorCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			string color = info.GetArg(1);
			player.SetNameColor(color, ActionPriority.Normal);

			char colorChar = ChatColor.GetValue(color, controller);
			info.ReplyToCommand($" {ChatColors.Green}Name color set to: {colorChar}Example{ChatColors.Default}");
		}

		private void HandleSetChatColorCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			string color = info.GetArg(1);
			player.SetChatColor(color, ActionPriority.Normal);

			char colorChar = ChatColor.GetValue(color, controller);
			info.ReplyToCommand($" {ChatColors.Green}Chat color set to: {colorChar}Example{ChatColors.Default}");
		}

		private void HandleSetClanTagCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			string tag = info.GetArg(1);
			player.SetClanTag(tag, ActionPriority.Normal);

			info.ReplyToCommand($" {ChatColors.Green}Clan tag set to: '{tag}'");
			info.ReplyToCommand($" {ChatColors.Blue}Tip: Clan tags support placeholders like {{kills}}, {{deaths}}, {{score}}");
			info.ReplyToCommand($" {ChatColors.Blue}The ChatProcessor will automatically replace placeholders every 3 seconds");
		}

		private void HandleGagCommand(CCSPlayerController? controller, CommandInfo info)
		{
			var targetName = info.GetArg(1);
			var target = Utilities.GetPlayers().FirstOrDefault(p =>
				p.IsValid && p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));

			if (target == null)
			{
				info.ReplyToCommand($" {ChatColors.Red}Player '{targetName}' not found");
				return;
			}

			var player = Player.Find(target);
			if (player == null) return;

			player.SetGag(true, ActionPriority.Normal);
			info.ReplyToCommand($" {ChatColors.Green}Player '{target.PlayerName}' has been gagged (text chat blocked)");
		}

		private void HandleUngagCommand(CCSPlayerController? controller, CommandInfo info)
		{
			var targetName = info.GetArg(1);
			var target = Utilities.GetPlayers().FirstOrDefault(p =>
				p.IsValid && p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));

			if (target == null)
			{
				info.ReplyToCommand($" {ChatColors.Red}Player '{targetName}' not found");
				return;
			}

			var player = Player.Find(target);
			if (player == null) return;

			player.SetGag(false, ActionPriority.Normal);
			info.ReplyToCommand($" {ChatColors.Green}Player '{target.PlayerName}' has been ungagged");
		}

		private void HandleMuteCommand(CCSPlayerController? controller, CommandInfo info)
		{
			var targetName = info.GetArg(1);
			var target = Utilities.GetPlayers().FirstOrDefault(p =>
				p.IsValid && p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));

			if (target == null)
			{
				info.ReplyToCommand($" {ChatColors.Red}Player '{targetName}' not found");
				return;
			}

			var player = Player.Find(target);
			if (player == null) return;

			player.SetMute(true, ActionPriority.Normal);
			info.ReplyToCommand($" {ChatColors.Green}Player '{target.PlayerName}' has been muted (voice chat blocked)");
		}

		private void HandleUnmuteCommand(CCSPlayerController? controller, CommandInfo info)
		{
			var targetName = info.GetArg(1);
			var target = Utilities.GetPlayers().FirstOrDefault(p =>
				p.IsValid && p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));

			if (target == null)
			{
				info.ReplyToCommand($" {ChatColors.Red}Player '{targetName}' not found");
				return;
			}

			var player = Player.Find(target);
			if (player == null) return;

			player.SetMute(false, ActionPriority.Normal);
			info.ReplyToCommand($" {ChatColors.Green}Player '{target.PlayerName}' has been unmuted");
		}

		private void HandleResetChatCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			// Reset all chat modifications
			player.SetNameTag(null!, ActionPriority.Normal);
			player.SetNameColor(null!, ActionPriority.Normal);
			player.SetChatColor(null!, ActionPriority.Normal);
			player.SetClanTag(null!, ActionPriority.Normal);
			player.SetGag(false, ActionPriority.Normal);
			player.SetMute(false, ActionPriority.Normal);

			info.ReplyToCommand($" {ChatColors.Green}All chat modifications have been reset");
		}

		private void HandleResetColorsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			// Reset only colors
			player.SetNameColor(null!, ActionPriority.Normal);
			player.SetChatColor(null!, ActionPriority.Normal);

			info.ReplyToCommand($" {ChatColors.Green}Chat colors have been reset to default");
		}

		private void HandleToggleChatModCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			// Toggle chat modifier processing
			player.EnableChatModifiers = !player.EnableChatModifiers;

			var status = player.EnableChatModifiers ? "enabled" : "disabled";
			var color = player.EnableChatModifiers ? ChatColors.Green : ChatColors.Red;

			info.ReplyToCommand($" {ChatColors.Blue}Chat modifiers {color}{status}");
			info.ReplyToCommand($" {ChatColors.Grey}This affects whether your custom tags and colors are shown");
		}

		private void HandleColorsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			info.ReplyToCommand($" {ChatColors.Gold}=== Available Colors ===");
			info.ReplyToCommand($" {ChatColors.White}Basic Colors:");
			info.ReplyToCommand($" {ChatColors.Red}red {ChatColors.Green}green {ChatColors.Blue}blue {ChatColors.Yellow}yellow");
			info.ReplyToCommand($" {ChatColors.Purple}purple {ChatColors.Lime}lime {ChatColors.Orange}orange {ChatColors.Grey}grey");
			info.ReplyToCommand($" {ChatColors.White}Special Colors:");
			info.ReplyToCommand($" {ChatColors.Blue}team {ChatColors.White}(your team color) | random {ChatColors.White}(random color)");
			info.ReplyToCommand($" {ChatColors.Blue}Usage: set-namecolor red, set-chatcolor team, etc.");
		}

		protected override void SdkUnload(bool hotReload)
		{
			// ChatProcessor automatically uninjects this plugin when SdkUnload is called
			// The SDK handles the uninjection process through ChatProcessor.TryUninject()
			// If this was the active plugin, another registered plugin will be injected
		}
	}
}
