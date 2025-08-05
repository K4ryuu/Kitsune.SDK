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
using Kitsune.SDK.Utilities.Helpers;

namespace Kitsune.Examples
{
	/// <summary>
	/// Example showing storage handler functionality with incremental updates using type-safe API
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class StorageExample : SdkPlugin, ISdkStorage<PlayerData>
	{
		public override string ModuleName => "Kitsune Storage Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Example demonstrating storage handler with incremental updates";

		protected override void SdkLoad(bool hotReload)
		{
			// Register commands
			RegisterCommands();

			// Register events
			RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
			RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
			RegisterEventHandler<EventPlayerActivate>(OnPlayerConnect);
			RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
		}

		private void RegisterCommands()
		{
			// Basic stats command
			Commands.Register("stats", "View your stats", HandleStatsCommand);

			// Command to give credits (demonstrates AddStorage)
			Commands.Register("givecredits", "Give credits to yourself", HandleGiveCreditsCommand, argCount: 1, helpText: "<amount>", permission: "@kitsune/admin", usage: CommandUsage.CLIENT_ONLY);

			// Command to take credits (demonstrates TakeStorage)
			Commands.Register("takecredits", "Take credits from yourself", HandleTakeCreditsCommand, argCount: 1, helpText: "<amount>", permission: "@kitsune/admin", usage: CommandUsage.CLIENT_ONLY);

			// Command to add XP (demonstrates incremental)
			Commands.Register("addxp", "Add XP to yourself", HandleAddXpCommand, argCount: 1, helpText: "<amount>", permission: "@kitsune/admin", usage: CommandUsage.CLIENT_ONLY);

			// Command to set rank (demonstrates direct set)
			Commands.Register("setrank", "Set your rank", HandleSetRankCommand, argCount: 1, helpText: "<rank>", permission: "@kitsune/admin", usage: CommandUsage.CLIENT_ONLY);

			// Command to add achievement
			Commands.Register("achievement", "Grant an achievement", HandleAchievementCommand, argCount: 1, helpText: "<name>", permission: "@kitsune/admin", usage: CommandUsage.CLIENT_ONLY);

			// Commands to demonstrate reset functionality
			Commands.Register("resetstorage", "Reset your storage to default values", HandleResetCommand);

			// Command to demonstrate cross-plugin access
			Commands.Register("crossdata", "Access cross-plugin storage", HandleCrossDataCommand);
		}

		private void HandleStatsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			var player = Player.Find(controller);
			if (player == null) return;

			// Get player data using type-safe storage
			var data = player.Storage<PlayerData>();

			info.ReplyToCommand($" {ChatColors.Gold}=== Your Stats ===");
			info.ReplyToCommand($" {ChatColors.Green}K/D: {data.Kills}/{data.Deaths} | Rank: {data.Rank}");
			info.ReplyToCommand($" {ChatColors.Blue}Credits: {data.Credits} | XP: {data.XP} | Level: {data.Level}");

			if (data.Stats.Count > 0)
			{
				info.ReplyToCommand($" {ChatColors.Yellow}Headshots: {data.Stats.GetValueOrDefault("headshots", 0)}");
				info.ReplyToCommand($" {ChatColors.Yellow}Assists: {data.Stats.GetValueOrDefault("assists", 0)}");
				info.ReplyToCommand($" {ChatColors.Yellow}MVPs: {data.Stats.GetValueOrDefault("mvps", 0)}");
			}
		}

		private void HandleGiveCreditsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string amountStr = info.GetArg(1);

			if (!int.TryParse(amountStr, out int amount) || amount <= 0)
			{
				info.ReplyToCommand($" {ChatColors.Red}Invalid amount: {amountStr}");
				return;
			}

			var player = Player.Find(controller);
			if (player == null) return;

			// Use type-safe storage for incremental update
			var data = player.Storage<PlayerData>();
			data.Credits += amount;

			info.ReplyToCommand($" {ChatColors.Green}Gave {amount} credits to yourself");
			info.ReplyToCommand($" {ChatColors.Blue}Your new balance: {data.Credits} credits");
		}

		private void HandleTakeCreditsCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string amountStr = info.GetArg(1);

			if (!int.TryParse(amountStr, out int amount) || amount <= 0)
			{
				info.ReplyToCommand($" {ChatColors.Red}Invalid amount: {amountStr}");
				return;
			}

			var player = Player.Find(controller);
			if (player == null) return;

			// Get player data
			var data = player.Storage<PlayerData>();

			// Check current balance
			if (data.Credits < amount)
			{
				info.ReplyToCommand($" {ChatColors.Red}You don't have enough credits to take {amount}");
				return;
			}

			// Take credits
			data.Credits -= amount;

			info.ReplyToCommand($" {ChatColors.Green}Took {amount} credits from yourself");
			info.ReplyToCommand($" {ChatColors.Blue}Your new balance: {data.Credits} credits");
		}

		private void HandleAddXpCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string amountStr = info.GetArg(1);

			if (!int.TryParse(amountStr, out int amount) || amount <= 0)
			{
				info.ReplyToCommand($" {ChatColors.Red}Invalid amount: {amountStr}");
				return;
			}

			var player = Player.Find(controller);
			if (player == null) return;

			// Get player data
			var data = player.Storage<PlayerData>();

			// Add XP
			data.XP += amount;

			// Check for level up
			var newLevel = (data.XP / 1000) + 1; // Simple level calculation

			if (newLevel > data.Level)
			{
				data.Level = newLevel;
				info.ReplyToCommand($" {ChatColors.Gold}LEVEL UP! You are now level {newLevel}!");

				// Give level bonus
				data.Credits += 500;
				info.ReplyToCommand($" {ChatColors.Green}Level bonus: +500 credits!");
			}

			info.ReplyToCommand($" {ChatColors.Green}Gave {amount} XP to yourself");
			info.ReplyToCommand($" {ChatColors.Blue}+{amount} XP! Total: {data.XP}");
		}

		private void HandleSetRankCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string rank = info.GetArg(2);

			var player = Player.Find(controller);
			if (player == null) return;

			// Get player data and set rank
			var data = player.Storage<PlayerData>();
			data.Rank = rank;

			info.ReplyToCommand($" {ChatColors.Green}Set your rank to: {rank}");
		}

		private void HandleAchievementCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			string achievement = info.GetArg(1);

			var player = Player.Find(controller);
			if (player == null) return;

			// Get player data
			var data = player.Storage<PlayerData>();

			if (data.Achievements.Contains(achievement))
			{
				info.ReplyToCommand($" {ChatColors.Yellow}You already have this achievement");
				return;
			}

			// Add achievement
			data.Achievements.Add(achievement);

			// Award XP for achievement
			data.XP += 250;

			info.ReplyToCommand($" {ChatColors.Green}Granted achievement '{achievement}' to yourself");
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
					// Use the new reset functionality
					await player.ResetStorage<PlayerData>();

					info.ReplyToCommand($" {ChatColors.Green}Your storage has been reset to default values!");
					info.ReplyToCommand($" {ChatColors.Yellow}Default values: Credits: 1000 | XP: 0 | Level: 1");
				});
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Error resetting storage: {ex.Message}");
			}
		}

		private void HandleCrossDataCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null || !controller.IsValid) return;

			var player = Player.Find(controller);
			if (player == null) return;

			try
			{
				// Example: Access data from another plugin using dynamic storage
				dynamic k4Storage = player.Storage("k4-stats");
				int k4Points = k4Storage.points ?? 0;
				string k4Rank = k4Storage.rank ?? "None";

				info.ReplyToCommand($" {ChatColors.Gold}=== Cross-Plugin Data ===");
				info.ReplyToCommand($" {ChatColors.Blue}K4 Points: {k4Points}");
				info.ReplyToCommand($" {ChatColors.Blue}K4 Rank: {k4Rank}");

				// Example: Modify another plugin's data (if allowed)
				k4Storage.points = k4Points + 10;
				info.ReplyToCommand($" {ChatColors.Green}Added 10 points to K4 stats");
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Error accessing cross-plugin data: {ex.Message}");
			}
		}

		private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
		{
			var victim = @event.Userid;
			var attacker = @event.Attacker;

			if (victim != null && victim.IsValid)
			{
				var victimPlayer = Player.Find(victim);
				if (victimPlayer != null)
				{
					// Get victim data and update
					var victimData = victimPlayer.Storage<PlayerData>();
					victimData.Deaths++;

					// Lose some credits on death
					victimData.Credits = Math.Max(0, victimData.Credits - 50);
				}
			}

			if (attacker != null && attacker.IsValid && attacker != victim)
			{
				var attackerPlayer = Player.Find(attacker);
				if (attackerPlayer != null)
				{
					// Get attacker data and update
					var attackerData = attackerPlayer.Storage<PlayerData>();
					attackerData.Kills++;

					// Give rewards
					attackerData.Credits += 100;
					attackerData.XP += 25;

					// Update detailed stats
					if (@event.Headshot)
					{
						attackerData.Stats["headshots"] = attackerData.Stats.GetValueOrDefault("headshots", 0) + 1;
						attackerData.XP += 10; // Bonus XP for headshot
					}
				}
			}

			return HookResult.Continue;
		}

		private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
		{
			// Give round bonus to all players
			foreach (var player in PlayerEx.GetValidPlayers())
			{
				// Get player data
				var data = player.Storage<PlayerData>();

				// Round participation bonus
				data.Credits += 50;
				data.XP += 15;

				// Update playtime
				data.Playtime += 3; // 3 minutes per round estimate
			}

			return HookResult.Continue;
		}

		private HookResult OnPlayerConnect(EventPlayerActivate @event, GameEventInfo info)
		{
			var player = @event.Userid;
			if (player == null || !player.IsValid || player.IsBot)
				return HookResult.Continue;

			// ! This is going to handle all the loading and initialization of every resource, its a simple oneliner but you can extend it
			// ! Also if a plugin already created the player and called load on its storage or settings, you receive the same instance
			// ! This means that all player loaded once, using one single connection to all these data across all plugins using SDK
			Player.GetOrCreate<Player>(player);

			return HookResult.Continue;
		}

		private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			// ! This is going to handle save and full disposal of every resource, its a simple oneliner but you can extend it
			Player.Find(@event.Userid)?.Dispose();
			return HookResult.Continue;
		}

		protected override void SdkUnload(bool hotReload)
		{
			// ! Resource cleanup is automatically handled by SdkPlugin
		}
	}

	/// <summary>
	/// Player data model with type-safe properties
	/// </summary>
	public class PlayerData
	{
		[Storage("kills", "Player's kill count")]
		public int Kills { get; set; } = 0;

		[Storage("deaths", "Player's death count")]
		public int Deaths { get; set; } = 0;

		// Track external changes (e.g., web panel modifications)
		// Lets say you have 50 credits when you join and you lose 25, without this if you purchase on a webshop 100 credit and then you leave
		/// you will have 25 credits in the database, because it overrides the value. With the tracker the storage SYNCHRONIZES the value on all save
		/// plus it will respect the changes of any external source. So now when you leave, instead of 25, the result would be 125 credits

		// Note: Only use this if you expect the data to be modified by external sources
		// For example, if another plugin or webshop modifies this plugin's data
		// You can also use this with other SDK user plugin storages like "k4-stats:kills" or so
		[Storage("credits", "Player's credits", track: true)]
		public int Credits { get; set; } = 1000;

		[Storage("xp", "Player's experience points")]
		public int XP { get; set; } = 0;

		[Storage("level", "Player's level")]
		public int Level { get; set; } = 1;

		[Storage("playtime", "Player's playtime in minutes")]
		public int Playtime { get; set; } = 0;

		[Storage("rank", "Player's rank")]
		public string Rank { get; set; } = "Bronze";

		[Storage("achievements", "Player's achievements")]
		public List<string> Achievements { get; set; } = new List<string>();

		[Storage("stats", "Player's detailed statistics")]
		public Dictionary<string, int> Stats { get; set; } = new Dictionary<string, int>
		{
			["headshots"] = 0,
			["assists"] = 0,
			["mvps"] = 0
		};
	}
}