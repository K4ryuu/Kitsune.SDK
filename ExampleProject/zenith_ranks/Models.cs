using Kitsune.SDK.Core.Attributes.Config;
using Kitsune.SDK.Core.Attributes.Data;
using Kitsune.SDK.Services;
using System.Text.Json.Serialization;

namespace K4_Zenith_Ranks;

// Storage model - Only stores what's necessary in database
public class PlayerRankData : StorageBase
{
    [Storage("Points", "Player's rank points", track: true)]
    public long Points { get => Get<long>(); set => Set(value); }

    [Storage("Rank", "Player's current rank name")]
    public string Rank { get => Get<string>() ?? "k4.phrases.rank.none"; set => Set(value); }

    // In-memory only data (not stored in database)
    [JsonIgnore]
    public KillStreakInfo KillStreak { get; set; } = new();

    [JsonIgnore]
    public int RoundPoints { get; set; }

    [JsonIgnore]
    public bool HasSpawned { get; set; }
}

// Settings model - Player preferences
public class PlayerSettings : SettingsBase
{
    [Setting("show_rank_changes", "Show rank change notifications")]
    public bool ShowRankChanges { get => Get<bool>(); set => Set(value); }
}

// Configuration models
public class RanksConfig
{
    [ConfigGroup("points")]
    public PointsConfig Points { get; set; } = new();

    [ConfigGroup("settings")]
    public SettingsConfig Settings { get; set; } = new();
}

public class PointsConfig
{
    [Config("start_points", "Starting points for new players")]
    public long StartPoints { get; set; } = 0;

    [Config("max_points", "Maximum points (0 = unlimited)")]
    public long MaxPoints { get; set; } = 0;

    // Kill/Death points
    [Config("kill", "Points for kill")]
    public int Kill { get; set; } = 6;

    [Config("death", "Points for death")]
    public int Death { get; set; } = -6;

    [Config("teamkill", "Points for team kill")]
    public int TeamKill { get; set; } = -12;

    [Config("suicide", "Points for suicide")]
    public int Suicide { get; set; } = -7;

    [Config("assist", "Points for assist")]
    public int Assist { get; set; } = 3;

    [Config("assist_flash", "Points for flash assist")]
    public int AssistFlash { get; set; } = 4;

    // Special kill bonuses
    [Config("headshot", "Bonus points for headshot")]
    public int Headshot { get; set; } = 4;

    [Config("penetrated", "Bonus points for penetration kill")]
    public int Penetrated { get; set; } = 2;

    [Config("noscope", "Bonus points for no-scope kill")]
    public int NoScope { get; set; } = 10;

    [Config("thrusmoke", "Bonus points for kill through smoke")]
    public int Thrusmoke { get; set; } = 8;

    [Config("blindkill", "Bonus points for blind kill")]
    public int BlindKill { get; set; } = 3;

    [Config("longdistance_kill", "Bonus points for long distance kill")]
    public int LongDistanceKill { get; set; } = 6;

    [Config("longdistance", "Distance for long distance kill (units)")]
    public int LongDistance { get; set; } = 30;

    // Weapon specific
    [Config("knife_kill", "Points for knife kill")]
    public int KnifeKill { get; set; } = 12;

    [Config("taser_kill", "Points for taser kill")]
    public int TaserKill { get; set; } = 15;

    [Config("grenade_kill", "Points for grenade kill")]
    public int GrenadeKill { get; set; } = 20;

    [Config("inferno_kill", "Points for molotov/incendiary kill")]
    public int InfernoKill { get; set; } = 20;

    [Config("impact_kill", "Points for impact kill")]
    public int ImpactKill { get; set; } = 50;

    // Round events
    [Config("round_win", "Points for round win")]
    public int RoundWin { get; set; } = 3;

    [Config("round_lose", "Points for round loss")]
    public int RoundLose { get; set; } = -3;

    [Config("mvp", "Points for MVP")]
    public int MVP { get; set; } = 8;

    // Bomb events
    [Config("bomb_plant", "Points for planting bomb")]
    public int BombPlant { get; set; } = 7;

    [Config("bomb_defused", "Points for defusing bomb")]
    public int BombDefused { get; set; } = 8;

    [Config("bomb_defused_others", "Points for others when bomb is defused")]
    public int BombDefusedOthers { get; set; } = 2;

    [Config("bomb_exploded", "Points for bomb explosion")]
    public int BombExploded { get; set; } = 7;

    [Config("bomb_pickup", "Points for picking up bomb")]
    public int BombPickup { get; set; } = 1;

    [Config("bomb_drop", "Points for dropping bomb")]
    public int BombDrop { get; set; } = -3;

    // Hostage events
    [Config("hostage_rescue", "Points for rescuing hostage")]
    public int HostageRescue { get; set; } = 12;

    [Config("hostage_rescue_all", "Extra points for rescuing all hostages")]
    public int HostageRescueAll { get; set; } = 8;

    [Config("hostage_hurt", "Points for hurting hostage")]
    public int HostageHurt { get; set; } = -3;

    [Config("hostage_kill", "Points for killing hostage")]
    public int HostageKill { get; set; } = -25;

    // Kill streaks
    [Config("seconds_between_kills", "Seconds between kills for multi-kill (0 = disabled)")]
    public int SecondsBetweenKills { get; set; } = 0;

    [Config("round_end_killstreak_reset", "Reset kill streak on round end")]
    public bool RoundEndKillStreakReset { get; set; } = true;

    [Config("double_kill", "Points for double kill")]
    public int DoubleKill { get; set; } = 4;

    [Config("triple_kill", "Points for triple kill")]
    public int TripleKill { get; set; } = 8;

    [Config("domination", "Points for domination (4 kills)")]
    public int Domination { get; set; } = 12;

    [Config("rampage", "Points for rampage (5 kills)")]
    public int Rampage { get; set; } = 16;

    [Config("mega_kill", "Points for mega kill (6 kills)")]
    public int MegaKill { get; set; } = 20;

    [Config("ownage", "Points for ownage (7 kills)")]
    public int Ownage { get; set; } = 24;

    [Config("ultra_kill", "Points for ultra kill (8 kills)")]
    public int UltraKill { get; set; } = 28;

    [Config("killing_spree", "Points for killing spree (9 kills)")]
    public int KillingSpree { get; set; } = 32;

    [Config("monster_kill", "Points for monster kill (10 kills)")]
    public int MonsterKill { get; set; } = 36;

    [Config("unstoppable", "Points for unstoppable (11 kills)")]
    public int Unstoppable { get; set; } = 40;

    [Config("godlike", "Points for godlike (12+ kills)")]
    public int GodLike { get; set; } = 45;

    // Playtime
    [Config("playtime_interval", "Minutes between playtime rewards (0 = disabled)")]
    public int PlaytimeInterval { get; set; } = 10;

    [Config("playtime_points", "Points for playtime interval")]
    public int PlaytimePoints { get; set; } = 10;

    // Team assist penalties
    [Config("teamkill_assist", "Points for team kill assist")]
    public int TeamKillAssist { get; set; } = -5;

    [Config("teamkill_assist_flash", "Points for team kill flash assist")]
    public int TeamKillAssistFlash { get; set; } = -3;
}

public class SettingsConfig
{
    [Config("warmup_points", "Allow points during warmup")]
    public bool WarmupPoints { get; set; } = false;

    [Config("point_summaries", "Show point summary at round end instead of real-time")]
    public bool PointSummaries { get; set; } = false;

    [Config("enable_requirement_messages", "Show messages when point requirements not met")]
    public bool EnableRequirementMessages { get; set; } = true;

    [Config("min_players", "Minimum players for points")]
    public int MinPlayers { get; set; } = 4;

    [Config("points_for_bots", "Allow points for bot kills/deaths")]
    public bool PointsForBots { get; set; } = false;

    [Config("scoreboard_score_sync", "Sync points with scoreboard score")]
    public bool ScoreboardScoreSync { get; set; } = false;

    [Config("vip_multiplier", "Point multiplier for VIP players")]
    public double VipMultiplier { get; set; } = 1.25;

    [Config("vip_flags", "Admin flags for VIP multiplier")]
    public List<string> VipFlags { get; set; } = ["@k4-ranks/vip"];

    [Config("dynamic_death_points", "Calculate death points based on rank difference")]
    public bool DynamicDeathPoints { get; set; } = true;

    [Config("dynamic_death_points_max_multiplier", "Max multiplier for dynamic death points")]
    public double DynamicDeathPointsMaxMultiplier { get; set; } = 3.0;

    [Config("dynamic_death_points_min_multiplier", "Min multiplier for dynamic death points")]
    public double DynamicDeathPointsMinMultiplier { get; set; } = 0.5;

    [Config("use_scoreboard_ranks", "Show rank images on scoreboard")]
    public bool UseScoreboardRanks { get; set; } = true;

    [Config("show_rank_changes", "Globally enable rank change notifications")]
    public bool ShowRankChanges { get; set; } = true;

    [Config("scoreboard_mode", "Scoreboard rank mode (1=premier, 2=competitive, 3=wingman, 4=dangerzone, 0=custom)")]
    public int ScoreboardMode { get; set; } = 1;

    [Config("rank_base", "Base rank value for custom mode")]
    public int RankBase { get; set; } = 0;

    [Config("rank_max", "Maximum rank value for custom mode")]
    public int RankMax { get; set; } = 0;

    [Config("rank_margin", "Rank margin for custom mode")]
    public int RankMargin { get; set; } = 0;

    [Config("extended_death_messages", "Include enemy name and points in death messages")]
    public bool ExtendedDeathMessages { get; set; } = true;

    // UI and command settings (moved from general)
    [Config("rank_commands", "Commands to show player rank")]
    public List<string> RankCommands { get; set; } = ["rank", "level"];

    [Config("use_html_messages", "Use HTML center messages instead of chat")]
    public bool UseHtmlMessages { get; set; } = true;

    [Config("center_message_time", "Time to display center messages (seconds)")]
    public int CenterMessageTime { get; set; } = 5;
}


// Rank definition model (loaded from ranks.jsonc)
public class Rank
{
    public int Id { get; set; }

    [JsonPropertyName("Name")]
    public required string Name { get; set; }

    [JsonPropertyName("Point")]
    public int Point { get; set; }

    [JsonPropertyName("ChatColor")]
    public string ChatColor { get; set; } = "default";

    [JsonPropertyName("HexColor")]
    public string HexColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("Image")]
    public string? Image { get; set; }
}

public class Permission
{
    [JsonPropertyName("DisplayName")]
    public required string DisplayName { get; set; }

    [JsonPropertyName("PermissionName")]
    public required string PermissionName { get; set; }
}

// Helper classes
public class KillStreakInfo
{
    public int KillCount { get; set; }
    public long LastKillTime { get; set; }

    public void Reset()
    {
        KillCount = 0;
        LastKillTime = 0;
    }
}