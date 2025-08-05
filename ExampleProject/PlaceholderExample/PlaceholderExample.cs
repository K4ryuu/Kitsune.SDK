using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Services;

namespace Kitsune.Examples
{
	/// <summary>
	/// Example showing placeholder handler functionality
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class PlaceholderExample : SdkPlugin
	{
		public override string ModuleName => "Kitsune Placeholder Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Example demonstrating placeholder handler usage";

		protected override void SdkLoad(bool hotReload)
		{
			// When a plugin calls the placeholder handler, it will be registered
			// All placeholders are registered in the global dictionary

			// Register placeholders
			RegisterPlayers();
			RegisterServers();

			// Register commands
			Registers();
		}

		private void RegisterPlayers()
		{
			// Basic player info
			Placeholders.RegisterPlayer("{player_name}", player => player.PlayerName);
			Placeholders.RegisterPlayer("{player_steamid}", player => player.SteamID.ToString());
			Placeholders.RegisterPlayer("{player_userid}", player => player.UserId?.ToString() ?? "0");
			Placeholders.RegisterPlayer("{player_ping}", player => player.Ping.ToString());

			// Team related
			Placeholders.RegisterPlayer("{player_team}", player =>
			{
				return player.TeamNum switch
				{
					2 => "Terrorist",
					3 => "Counter-Terrorist",
					_ => "Spectator"
				};
			});

			Placeholders.RegisterPlayer("{player_team_color}", player =>
			{
				return player.TeamNum switch
				{
					2 => ChatColors.Red.ToString(),
					3 => ChatColors.Blue.ToString(),
					_ => ChatColors.Default.ToString()
				};
			});

			// Health and armor
			Placeholders.RegisterPlayer("{player_health}", player => player.PlayerPawn?.Value?.Health.ToString() ?? "0");

			Placeholders.RegisterPlayer("{player_armor}", player => player.PlayerPawn?.Value?.ArmorValue.ToString() ?? "0");

			// Weapon info
			Placeholders.RegisterPlayer("{player_weapon}", player =>
			{
				var activeWeapon = player.PlayerPawn?.Value?.WeaponServices?.ActiveWeapon?.Value;
				return activeWeapon?.DesignerName ?? "none";
			});

			// Location
			Placeholders.RegisterPlayer("{player_location}", player =>
			{
				var pos = player.PlayerPawn?.Value?.AbsOrigin;
				if (pos != null)
				{
					return $"{pos.X:F0},{pos.Y:F0},{pos.Z:F0}";
				}

				return "unknown";
			});
		}

		private void RegisterServers()
		{
			// Server info
			Placeholders.RegisterServer("{server_name}", () => "My Awesome Server");
			Placeholders.RegisterServer("{server_time}", () => DateTime.Now.ToString("HH:mm:ss"));
			Placeholders.RegisterServer("{server_date}", () => DateTime.Now.ToString("yyyy-MM-dd"));
			Placeholders.RegisterServer("{map}", () => Server.MapName);

			// ! The SDK automatically handles errors and returns "N/A" if placeholders fail (e.g., NativeException)
			Placeholders.RegisterServer("{player_count}", () =>
			{
				var players = Utilities.GetPlayers().Where(p => p.IsValid);
				return players.Count(p => !p.IsBot).ToString();
			});

			Placeholders.RegisterServer("{bot_count}", () =>
			{
				var players = Utilities.GetPlayers().Where(p => p.IsValid);
				return players.Count(p => p?.IsBot == true).ToString();
			});

			Placeholders.RegisterServer("{max_players}", () =>
			{
				return Server.MaxPlayers.ToString();
			});

			Placeholders.RegisterServer("{ct_count}", () =>
			{
				var players = Utilities.GetPlayers().Where(p => p.IsValid);
				return players.Count(p => p?.Team == CsTeam.CounterTerrorist).ToString();
			});

			Placeholders.RegisterServer("{t_count}", () =>
			{
				var players = Utilities.GetPlayers().Where(p => p.IsValid);
				return players.Count(p => p?.Team == CsTeam.Terrorist).ToString();
			});

			// Random values
			Placeholders.RegisterServer("{random_number}", () => Random.Shared.Next(1, 100).ToString());
			Placeholders.RegisterServer("{random_player}", () =>
			{
				var players = Utilities.GetPlayers().Where(p => p.IsValid);
				if (players.Any())
				{
					return players.ElementAt(Random.Shared.Next(players.Count())).PlayerName;
				}
				return "No Players";
			});
		}

		private void Registers()
		{
			// Test player placeholder replacement
			Commands.Register("test-player-placeholders", "Test placeholder replacement", HandlePlayerTestCommand, usage: CommandUsage.CLIENT_ONLY);

			// Test server placeholder replacement
			Commands.Register("test-server-placeholders", "Test placeholder replacement", HandleServerTestCommand, usage: CommandUsage.CLIENT_AND_SERVER);

			// Test both placeholder replacement
			Commands.Register("test-placeholders", "Test placeholder replacement", HandleTestCommand, usage: CommandUsage.CLIENT_ONLY);

			// Show all placeholders
			Commands.Register("placeholders", "Show available placeholders", HandleListCommand);
		}

		private void HandlePlayerTestCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string text = info.GetCommandString.Replace(info.GetArg(1), string.Empty).Trim();

			// Replace placeholders
			string result = PlaceholderHandler.ReplacePlayerPlaceholders(text, controller);

			info.ReplyToCommand($" {ChatColors.Gold}Original: {text}");
			info.ReplyToCommand($" {ChatColors.Green}Result: {result}");
		}

		private void HandleServerTestCommand(CCSPlayerController? controller, CommandInfo info)
		{
			string text = info.GetCommandString.Replace(info.GetArg(1), string.Empty).Trim();

			// Replace placeholders
			string result = PlaceholderHandler.ReplaceServerPlaceholders(text);

			info.ReplyToCommand($" {ChatColors.Gold}Original: {text}");
			info.ReplyToCommand($" {ChatColors.Green}Result: {result}");
		}

		private void HandleTestCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string text = info.GetCommandString.Replace(info.GetArg(1), string.Empty).Trim();

			// Replace placeholders
			string result = PlaceholderHandler.ReplaceAll(text, controller);

			info.ReplyToCommand($" {ChatColors.Gold}Original: {text}");
			info.ReplyToCommand($" {ChatColors.Green}Result: {result}");
		}

		private void HandleListCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			info.ReplyToCommand($" {ChatColors.Gold}=== Available Placeholders ===");

			// Player placeholders for this plugin
			var playerPlaceholders = Placeholders.GetPlayerPlaceholders();
			info.ReplyToCommand($" {ChatColors.Blue}Player ({playerPlaceholders.Count}):");
			foreach (var ph in playerPlaceholders.Take(5))
			{
				info.ReplyToCommand($" {ChatColors.Green}  {ph.Placeholder}");
			}

			if (playerPlaceholders.Count > 5)
			{
				info.ReplyToCommand($" {ChatColors.Grey}  ... and {playerPlaceholders.Count - 5} more");
			}

			// Server placeholders for this plugin
			var serverPlaceholders = Placeholders.GetServerPlaceholders();
			info.ReplyToCommand($" {ChatColors.Blue}Server ({serverPlaceholders.Count}):");
			foreach (var ph in serverPlaceholders.Take(5))
			{
				info.ReplyToCommand($" {ChatColors.Green}  {ph.Placeholder}");
			}

			if (serverPlaceholders.Count > 5)
			{
				info.ReplyToCommand($" {ChatColors.Grey}  ... and {serverPlaceholders.Count - 5} more");
			}

			// Global placeholders
			var allPlayer = PlaceholderHandler.GetAllPlayerPlaceholders();
			var allServer = PlaceholderHandler.GetAllServerPlaceholders();

			info.ReplyToCommand($" {ChatColors.Yellow}Total: {allPlayer.Count} player, {allServer.Count} server placeholders");
		}

		protected override void SdkUnload(bool hotReload)
		{
			// ! Resource cleanup is automatically handled by SdkPlugin
		}
	}
}