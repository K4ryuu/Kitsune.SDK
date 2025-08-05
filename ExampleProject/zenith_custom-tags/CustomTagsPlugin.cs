using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

using SdkPlayer = Kitsune.SDK.Core.Base.Player;

namespace K4Zenith.CustomTags;

[MinimumApiVersion(300)]
[MinimumSdkVersion(1)]
public sealed class CustomTagsPlugin : SdkPlugin, ISdkConfig<CustomTagsConfig>, ISdkStorage<PlayerTagStorage>, ISdkSettings<PlayerTagSettings>
{
	public override string ModuleName => "K4-Zenith | CustomTags";
	public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
	public override string ModuleVersion => "2.0.0";

	public new CustomTagsConfig Config => GetTypedConfig<CustomTagsConfig>();

	private Dictionary<string, TagInfo> _availableTags = [];

	protected override void SdkLoad(bool hotReload)
	{
		ChatProcessor.TryInject(this);

		LoadTagsConfig();
		RegisterCommands();
		RegisterEvents();
		RegisterPlaceholders();
	}

	private void LoadTagsConfig()
	{
		var configPath = Path.Combine(ModuleDirectory, "tags.json");

		if (!File.Exists(configPath))
		{
			CreateDefaultTagsConfig(configPath);
		}

		try
		{
			var json = File.ReadAllText(configPath);
			var options = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				PropertyNameCaseInsensitive = true
			};
			var tags = JsonSerializer.Deserialize<Dictionary<string, TagInfo>>(json, options) ?? [];
			_availableTags = tags;
			Logger.LogInformation("Loaded {count} tags from configuration.", _availableTags.Count);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to load tags configuration");
			_availableTags = CreateFallbackTags();
		}
	}

	private void CreateDefaultTagsConfig(string configPath)
	{
		var defaultTags = new Dictionary<string, TagInfo>
		{
			["player"] = new()
			{
				Name = "Player",
				ChatColor = "white",
				ClanTag = "Player | ",
				NameColor = "white",
				NameTag = "{white}[Player] ",
				RequiredPermissions = [],
				RequiredSteamIds = [],
				Priority = 1
			},
			["vip"] = new()
			{
				Name = "VIP",
				ChatColor = "gold",
				ClanTag = "VIP | ",
				NameColor = "gold",
				NameTag = "{gold}[VIP] ",
				RequiredPermissions = ["@css/vip"],
				RequiredSteamIds = [],
				Priority = 5
			},
			["admin"] = new()
			{
				Name = "Admin",
				ChatColor = "blue",
				ClanTag = "ADMIN | ",
				NameColor = "blue",
				NameTag = "{blue}[ADMIN] ",
				RequiredPermissions = ["@css/admin"],
				RequiredSteamIds = [],
				Priority = 10
			},
			["owner"] = new()
			{
				Name = "Owner",
				ChatColor = "lightred",
				ClanTag = "OWNER | ",
				NameColor = "lightred",
				NameTag = "{lightred}[OWNER] ",
				RequiredPermissions = ["@css/root"],
				RequiredSteamIds = [],
				Priority = 100
			},
			["special"] = new()
			{
				Name = "Special",
				ChatColor = "purple",
				ClanTag = "SPECIAL | ",
				NameColor = "purple",
				NameTag = "{purple}[SPECIAL] ",
				RequiredPermissions = [],
				RequiredSteamIds = ["76561198000000000", "76561198000000001"],
				Priority = 50
			}
		};

		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		File.WriteAllText(configPath, JsonSerializer.Serialize(defaultTags, options));
		Logger.LogInformation("Created default tags configuration at {path}", configPath);
	}

	private Dictionary<string, TagInfo> CreateFallbackTags()
	{
		return new Dictionary<string, TagInfo>
		{
			["player"] = new()
			{
				Name = "Player",
				ChatColor = "white",
				ClanTag = "Player | ",
				NameColor = "white",
				NameTag = "{white}[Player] ",
				RequiredPermissions = [],
				RequiredSteamIds = [],
				Priority = 1
			}
		};
	}

	private void RegisterCommands()
	{
		Commands.Register(["tags", "tag"], "Open tag selection menu", OnTagCommand, usage: CommandUsage.CLIENT_ONLY);
		Commands.Register("reloadtags", "Reload tag configurations", OnReloadTagsCommand, usage: CommandUsage.CLIENT_AND_SERVER, permission: "@zenith/root");
	}

	private void RegisterEvents()
	{
		RegisterEventHandler<EventPlayerActivate>(OnPlayerActivate);
		RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

		// Subscribe to SDK player data load event
		Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataLoad, OnPlayerDataLoaded, HookMode.Post);
	}

	private void RegisterPlaceholders()
	{
		Placeholders.RegisterPlayer("{player_tag}", GetPlayerTag);
		Placeholders.RegisterPlayer("{player_clan_tag}", GetPlayerClanTag);
		Placeholders.RegisterPlayer("{player_selected_tag}", GetSelectedTag);
	}

	private HookResult OnPlayerActivate(EventPlayerActivate @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !player.IsValid || player.IsBot)
			return HookResult.Continue;

		SdkPlayer.GetOrCreate<SdkPlayer>(player);
		return HookResult.Continue;
	}

	private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		SdkPlayer.Find(@event.Userid)?.Dispose();
		return HookResult.Continue;
	}

	private HookResult OnPlayerDataLoaded(PlayerDataEventArgs args)
	{
		var player = SdkPlayer.Find(args.SteamId);
		if (player?.Controller != null)
		{
			ApplyPlayerTag(player.Controller);
		}

		return HookResult.Continue;
	}

	private void OnTagCommand(CCSPlayerController? controller, CommandInfo info)
	{
		if (controller == null || !controller.IsValid)
		{
			info.ReplyToCommand("This command is only for players.");
			return;
		}

		var player = SdkPlayer.Find(controller);
		if (player?.IsLoaded != true)
		{
			info.ReplyToCommand($" {Localizer.ForPlayer(controller, "k4.general.prefix")} {Localizer.ForPlayer(controller, "k4.general.loading")}");
			return;
		}

		var settings = player.Settings<PlayerTagSettings>();
		if (settings.TagsDisabled)
		{
			player.PrintToChat($" {Localizer.ForPlayer(controller, "k4.general.prefix")} {Localizer.ForPlayer(controller, "customtags.disabled")}");
			return;
		}

		ShowTagMenu(player);
	}

	private void OnReloadTagsCommand(CCSPlayerController? controller, CommandInfo info)
	{
		LoadTagsConfig();

		var message = Localizer.ForPlayer(controller, "customtags.configs_reloaded");
		if (controller != null)
		{
			info.ReplyToCommand($" {Localizer.ForPlayer(controller, "k4.general.prefix")} {message}");
		}
		else
		{
			Logger.LogInformation(message);
		}

		// Reapply tags to all players
		var players = Utilities.GetPlayers();
		foreach (var player in players.Where(p => p?.IsValid == true && !p.IsBot && !p.IsHLTV))
		{
			ApplyPlayerTag(player);
		}
	}

	private void ShowTagMenu(SdkPlayer player)
	{
		var availableTags = GetAvailableTagsForPlayer(player.Controller);

		if (availableTags.Count == 0)
		{
			player.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "customtags.no_tags_available")}");
			return;
		}

		ShowChatMenu(player, availableTags);
	}

	private void ShowChatMenu(SdkPlayer player, List<string> availableTags)
	{
		var menu = new ChatMenu(Localizer.ForPlayer(player.Controller, "customtags.menu.title"));

		var storage = player.Storage<PlayerTagStorage>();

		// Add "Default" option (highest priority available)
		var isDefaultSelected = storage.SelectedTag == "default";
		var defaultPrefix = isDefaultSelected ? $"{ChatColors.Green}✓ " : $"{ChatColors.Gold}";
		menu.AddMenuOption($"{defaultPrefix}{Localizer.ForPlayer(player.Controller, "customtags.menu.default")}", (p, o) =>
		{
			var sdkPlayer = SdkPlayer.Find(p);
			if (sdkPlayer != null)
				SetDefaultTag(sdkPlayer);
		});

		// Add "None" option to remove tags
		menu.AddMenuOption($"{ChatColors.Red}{Localizer.ForPlayer(player.Controller, "customtags.menu.none")}", (p, o) =>
		{
			var sdkPlayer = SdkPlayer.Find(p);
			if (sdkPlayer != null)
				RemoveSelectedTag(sdkPlayer);
		});


		// Add available tags
		foreach (var tagKey in availableTags)
		{
			if (!_availableTags.TryGetValue(tagKey, out var tag))
			{
				Logger.LogWarning($"Tag '{tagKey}' not found in _availableTags");
				continue;
			}

			var isSelected = storage.SelectedTag == tagKey;

			var prefix = isSelected ? $"{ChatColors.Green}✓ " : $"{ChatColors.Gold}";
			var menuText = $"{prefix}{tag.Name}";

			if (string.IsNullOrEmpty(tag.Name))
			{
				Logger.LogWarning($"Tag '{tagKey}' has empty or null Name property");
				continue;
			}

			menu.AddMenuOption(menuText, (p, o) =>
			{
				var sdkPlayer = SdkPlayer.Find(p);
				if (sdkPlayer != null)
					ApplySelectedTag(sdkPlayer, tagKey);
			});
		}

		MenuManager.OpenChatMenu(player.Controller, menu);
	}

	private List<string> GetAvailableTagsForPlayer(CCSPlayerController player)
	{
		var availableTags = new List<string>();

		foreach (var kvp in _availableTags)
		{
			var tagKey = kvp.Key;
			var tag = kvp.Value;

			// Check permissions and SteamIDs
			if (HasAccessToTag(player, tag))
			{
				availableTags.Add(tagKey);
			}
		}

		// Sort by priority (higher first)
		return availableTags.OrderByDescending(key => _availableTags[key].Priority).ToList();
	}

	private static bool HasAccessToTag(CCSPlayerController player, TagInfo tag)
	{
		// Check SteamID access first
		if (tag.RequiredSteamIds.Count > 0)
		{
			if (tag.RequiredSteamIds.Contains(player.SteamID.ToString()))
			{
				return true;
			}
		}

		// Check permission access
		if (tag.RequiredPermissions.Count > 0)
		{
			return HasAnyPermission(player, tag.RequiredPermissions);
		}

		// If no specific requirements, everyone has access
		return tag.RequiredPermissions.Count == 0 && tag.RequiredSteamIds.Count == 0;
	}

	private static bool HasAnyPermission(CCSPlayerController player, List<string> permissions)
	{
		if (permissions.Count == 0) return true;
		if (AdminManager.PlayerHasPermissions(player, "@css/root")) return true;

		foreach (var permission in permissions)
		{
			if (permission.StartsWith('@'))
			{
				if (AdminManager.PlayerHasPermissions(player, permission))
					return true;
			}
			else if (permission.StartsWith('#'))
			{
				if (AdminManager.PlayerInGroup(player, permission))
					return true;
			}
		}

		return false;
	}

	private void ApplySelectedTag(SdkPlayer player, string tagKey)
	{
		if (!_availableTags.TryGetValue(tagKey, out var tag))
			return;

		// Store selection
		var storage = player.Storage<PlayerTagStorage>();
		storage.SelectedTag = tagKey;

		// Apply tag
		ApplyTag(player.Controller, tag);

		// Feedback
		player.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "customtags.tag_applied", tag.Name)}");
	}

	private void SetDefaultTag(SdkPlayer player)
	{
		// Store "default" selection
		var storage = player.Storage<PlayerTagStorage>();
		storage.SelectedTag = "default";

		// Apply highest priority available tag
		ApplyPlayerTag(player.Controller);

		// Feedback
		player.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "customtags.default_applied")}");
	}

	private void RemoveSelectedTag(SdkPlayer player)
	{
		// Store "none" selection
		var storage = player.Storage<PlayerTagStorage>();
		storage.SelectedTag = "none";

		// Remove all tags
		RemovePlayerTags(player);

		// Feedback
		player.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "customtags.tags_removed")}");
	}

	private void ApplyPlayerTag(CCSPlayerController controller)
	{

		var player = SdkPlayer.Find(controller);
		if (player?.IsLoaded != true)
		{
			return;
		}

		var settings = player.Settings<PlayerTagSettings>();
		if (settings.TagsDisabled)
		{
			return;
		}

		var storage = player.Storage<PlayerTagStorage>();
		var selectedTag = storage.SelectedTag;

		// Handle "none" selection
		if (selectedTag == "none")
		{
			RemovePlayerTags(player);
			return;
		}

		var availableTags = GetAvailableTagsForPlayer(controller);

		if (availableTags.Count == 0)
		{
			return;
		}

		TagInfo? tag = null;

		// Handle "default" selection - always use highest priority available tag
		if (selectedTag == "default")
		{
			selectedTag = availableTags[0]; // Highest priority
			tag = _availableTags[selectedTag];
		}
		else
		{
			// Check if selected specific tag is still available
			if (_availableTags.TryGetValue(selectedTag, out tag) && availableTags.Contains(selectedTag))
			{
				// Selected tag is available, use it
			}
			else
			{
				// Selected tag not available, reset to default and use highest priority
				selectedTag = availableTags[0];
				storage.SelectedTag = "default";
				tag = _availableTags[selectedTag];
			}
		}

		ApplyTag(controller, tag);
	}

	private static void ApplyTag(CCSPlayerController controller, TagInfo tag)
	{
		var player = SdkPlayer.Find(controller);
		if (player == null) return;

		if (!string.IsNullOrEmpty(tag.ChatColor))
			player.SetChatColor(tag.ChatColor);

		if (!string.IsNullOrEmpty(tag.ClanTag))
			player.SetClanTag(tag.ClanTag);

		if (!string.IsNullOrEmpty(tag.NameColor))
			player.SetNameColor(tag.NameColor);

		if (!string.IsNullOrEmpty(tag.NameTag))
			player.SetNameTag(tag.NameTag);
	}

	private static void RemovePlayerTags(SdkPlayer player)
	{
		player.SetChatColor(null);
		player.SetClanTag(null);
		player.SetNameColor(null);
		player.SetNameTag(null);
	}

	#region Placeholders

	private string GetPlayerTag(CCSPlayerController player)
	{
		var sdkPlayer = SdkPlayer.Find(player);
		return sdkPlayer?.GetNameTag() ?? string.Empty;
	}

	private string GetPlayerClanTag(CCSPlayerController player)
	{
		var sdkPlayer = SdkPlayer.Find(player);
		return sdkPlayer?.GetClanTag() ?? string.Empty;
	}

	private string GetSelectedTag(CCSPlayerController player)
	{
		var sdkPlayer = SdkPlayer.Find(player);
		if (sdkPlayer?.IsLoaded == true)
		{
			var storage = sdkPlayer.Storage<PlayerTagStorage>();
			return storage.SelectedTag;
		}
		return "player";
	}

	#endregion
}