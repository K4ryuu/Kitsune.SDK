using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Localization;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Args;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Services;
using Kitsune.SDK.Services.Config;
using Microsoft.Extensions.Logging;

using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using Player = Kitsune.SDK.Core.Base.Player;
using Kitsune.SDK.Utilities.Helpers;

namespace Kitsune.SDK.Utilities
{
	public static class ChatProcessor
	{
		private static readonly List<SdkPlugin> _registeredPlugins = [];
		private static SdkPlugin? _activePlugin = null;
		private static IStringLocalizer Localizer => SdkTranslations.Instance;
		private static Timer? _enforceTimer;

		public static async Task TryInjectAsync(SdkPlugin plugin)
		{
			try
			{
				// Register ChatProcessor configs when first used and wait for load
				await SdkInternalConfig.RegisterChatProcessorAsync(plugin);

				// Add plugin to registered list if not already present
				if (!_registeredPlugins.Contains(plugin))
				{
					_registeredPlugins.Add(plugin);

					Server.NextWorldUpdate(() =>
					{
						// Subscribe to plugin unload events to handle automatic cleanup
						plugin.Events.Subscribe<ModuleUnloadEventArgs>(EventType.ModuleUnload, args =>
						{
							if (args.ModuleName == plugin.ModuleName)
							{
								TryUninject(plugin);
							}
							return HookResult.Continue;
						}, HookMode.Pre);
					});
				}

				// If no active plugin, make this one active
				if (_activePlugin == null)
				{
					Server.NextWorldUpdate(() => SetActivePlugin(plugin));
				}
			}
			catch (Exception ex)
			{
				plugin.Logger.LogError(ex, "ChatProcessor: Failed to inject");
			}
		}

		public static void TryInject(SdkPlugin plugin)
		{
			_ = TryInjectAsync(plugin);
		}

		public static void TryUninject(SdkPlugin plugin)
		{
			// Remove plugin from registered list
			_registeredPlugins.Remove(plugin);

			// If this was the active plugin, find a replacement
			if (_activePlugin == plugin)
			{
				UnhookFromActivePlugin();
				_activePlugin = null;

				// Try to activate another registered plugin
				if (_registeredPlugins.Count > 0)
				{
					SetActivePlugin(_registeredPlugins[0]);
				}
			}
		}

		private static void SetActivePlugin(SdkPlugin plugin)
		{
			_activePlugin = plugin;

			// Register GeoIP placeholders when ChatProcessor becomes active
			// This is needed because default tags use {country_short} and {country_long}
			SdkInternalConfig.RegisterGeoIP(plugin);

			// Get enforce interval with default of 3.0f
			float enforceInterval = SdkInternalConfig.GetValue<float>("enforce_interval", "chatprocessor", 3.0f);

			_enforceTimer = plugin.AddTimer(enforceInterval, EnforceValues, TimerFlags.REPEAT);
			plugin.HookUserMessage(118, OnMessage, HookMode.Pre);
		}

		private static void UnhookFromActivePlugin()
		{
			if (_activePlugin != null)
			{
				try
				{
					_enforceTimer?.Kill();
					_enforceTimer = null;

					_activePlugin.UnhookUserMessage(118, OnMessage, HookMode.Pre);
				}
				catch
				{
					// Ignore unhook errors during cleanup
				}
			}
		}

		public static SdkPlugin? GetActivePlugin()
		{
			return _activePlugin;
		}

		public static IReadOnlyList<SdkPlugin> GetRegisteredPlugins()
		{
			return [.. _registeredPlugins];
		}

		private static void EnforceValues()
		{
			if (_activePlugin == null)
				return;

			foreach (var player in Player.ValidLoop())
			{
				if (player.IsMuted && player.Controller.VoiceFlags.HasFlag(VoiceFlags.Muted) == false)
				{
					player.Controller.VoiceFlags |= VoiceFlags.Muted;
				}

				string clanTag = player.GetClanTag();
				if (!string.IsNullOrEmpty(clanTag))
				{
					player.Controller.Clan = PlaceholderHandler.ReplacePlayerPlaceholders(clanTag, player.Controller);
					CounterStrikeSharp.API.Utilities.SetStateChanged(player.Controller, "CCSPlayerController", "m_szClan");
				}
			}
		}

		private static HookResult OnMessage(UserMessage um)
		{
			// Validate and extract player information
			if (!TryGetPlayerFromMessage(um, out var player) || player == null)
				return HookResult.Continue;

			// Check if player is gagged
			if (player.IsGagged)
				return HookResult.Stop;

			// Extract message components
			var messageData = ExtractMessageData(um);

			// Build formatted chat message
			var formattedMessage = BuildFormattedMessage(player, messageData);

			// Apply the formatted message
			um.SetString("messagename", formattedMessage);
			return HookResult.Changed;
		}

		private static bool TryGetPlayerFromMessage(UserMessage um, out Player? player)
		{
			player = null;

			try
			{
				int entityIndex = um.ReadInt("entityindex");
				var controller = CounterStrikeSharp.API.Utilities.GetPlayerFromIndex(entityIndex);
				player = Player.Find(controller);

				return player != null && player.IsValid && player.Controller != null && player.EnableChatModifiers;
			}
			catch
			{
				return false;
			}
		}

		private static MessageData ExtractMessageData(UserMessage um)
		{
			return new MessageData
			{
				PlayerName = um.ReadString("param1"),
				MessageText = um.ReadString("param2"),
				MessageName = um.ReadString("messagename"),
				IsTeamChat = !um.ReadString("messagename").Contains("All")
			};
		}

		private static string BuildFormattedMessage(Player player, MessageData messageData)
		{
			var controller = player.Controller!;

			// Build message components
			var components = new ChatMessageComponents
			{
				DeadTag = GetDeadTag(player, controller),
				TeamTag = GetTeamTag(controller, messageData.IsTeamChat),
				CustomTag = player.GetNameTag(),
				NameColor = player.GetNameColor(),
				ChatColor = player.GetChatColor(),
				Separator = GetSeparator(controller)
			};

			// Construct the final formatted message
			string rawMessage = $" {components.DeadTag}{components.TeamTag}{components.CustomTag}{components.NameColor}{messageData.PlayerName}{components.Separator}{components.ChatColor}{messageData.MessageText}";

			return ChatColor.ReplaceColors(rawMessage, controller);
		}

		private static string GetDeadTag(Player player, CCSPlayerController controller)
		{
			return player.IsAlive ? string.Empty : Localizer.ForPlayer(controller, "kitsune.sdk.tag.dead");
		}

		private static string GetTeamTag(CCSPlayerController controller, bool isTeamChat)
		{
			return isTeamChat ? TeamLocalizer(controller) : string.Empty;
		}

		private static string GetSeparator(CCSPlayerController controller)
		{
			return Localizer.ForPlayer(controller, "kitsune.sdk.tag.separator");
		}

		private readonly struct MessageData
		{
			public required string PlayerName { get; init; }
			public required string MessageText { get; init; }
			public required string MessageName { get; init; }
			public required bool IsTeamChat { get; init; }
		}

		private readonly struct ChatMessageComponents
		{
			public required string DeadTag { get; init; }
			public required string TeamTag { get; init; }
			public required string CustomTag { get; init; }
			public required char NameColor { get; init; }
			public required char ChatColor { get; init; }
			public required string Separator { get; init; }
		}

		private static string TeamLocalizer(CCSPlayerController player)
		{
			return player.Team switch
			{
				CsTeam.Spectator => Localizer.ForPlayer(player, "kitsune.sdk.tag.team.spectator"),
				CsTeam.Terrorist => Localizer.ForPlayer(player, "kitsune.sdk.tag.team.t"),
				CsTeam.CounterTerrorist => Localizer.ForPlayer(player, "kitsune.sdk.tag.team.ct"),
				_ => Localizer.ForPlayer(player, "kitsune.sdk.tag.team.unassigned"),
			};
		}
	}
}