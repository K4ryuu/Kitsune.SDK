using Kitsune.SDK.Core.Attributes.Config;
using Kitsune.SDK.Core.Attributes.Data;
using Kitsune.SDK.Services;

namespace K4Zenith.CustomTags;

#region Configuration

public class CustomTagsConfig
{
	// Currently no configuration needed - kept for future use
}

#endregion

#region Player Data

public class PlayerTagStorage : StorageBase
{
	[Storage("selected_tag", "Currently selected tag")]
	public string SelectedTag { get => Get<string>() ?? "default"; set => Set(value); }
}

public class PlayerTagSettings : SettingsBase
{
	[Setting("tags_disabled", "Disable tag")]
	public bool TagsDisabled { get => Get<bool>(); set => Set(value); }
}

#endregion

#region Tag Models

public class TagInfo
{
	public string Name { get; set; } = string.Empty;
	public string? ChatColor { get; set; }
	public string? ClanTag { get; set; }
	public string? NameColor { get; set; }
	public string? NameTag { get; set; }
	public List<string> RequiredPermissions { get; set; } = [];
	public List<string> RequiredSteamIds { get; set; } = [];
	public int Priority { get; set; } = 1;
}

#endregion