using Kitsune.SDK.Core.Attributes.Config;
using Kitsune.SDK.Core.Attributes.Data;
using Kitsune.SDK.Services;
using System.Text.Json.Serialization;

using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace K4Zenith.Stats;

// Configuration model
public class StatsConfig
{
    [Config("statistic_commands", "List of commands that shows player statistics")]
    public List<string> StatisticCommands { get; set; } = ["stats", "stat", "statistics"];

    [Config("warmup_stats", "Allow stats during warmup")]
    public bool WarmupStats { get; set; } = false;

    [Config("min_players", "Minimum players to collect stats")]
    public int MinPlayers { get; set; } = 4;

    [Config("stats_for_bots", "Allow stats for bots")]
    public bool StatsForBots { get; set; } = false;

    [Config("enable_requirement_messages", "Enable or disable the messages for stats being disabled")]
    public bool EnableRequirementMessages { get; set; } = true;

    [Config("use_html_menu", "Use HTML center menu instead of chat menu (requires KitsuneMenu)")]
    public bool UseHtmlMenu { get; set; } = true;
}

// Storage model
public class PlayerStatsData : StorageBase
{
    [Storage("Kills", "Player kills")]
    public int Kills { get => Get<int>(); set => Set(value); }

    [Storage("FirstBlood", "First blood kills")]
    public int FirstBlood { get => Get<int>(); set => Set(value); }

    [Storage("Deaths", "Player deaths")]
    public int Deaths { get => Get<int>(); set => Set(value); }

    [Storage("Assists", "Player assists")]
    public int Assists { get => Get<int>(); set => Set(value); }

    [Storage("Shoots", "Shots fired")]
    public int Shoots { get => Get<int>(); set => Set(value); }

    [Storage("HitsTaken", "Hits taken")]
    public int HitsTaken { get => Get<int>(); set => Set(value); }

    [Storage("HitsGiven", "Hits given")]
    public int HitsGiven { get => Get<int>(); set => Set(value); }

    [Storage("Headshots", "Headshot kills")]
    public int Headshots { get => Get<int>(); set => Set(value); }

    [Storage("HeadHits", "Head hits")]
    public int HeadHits { get => Get<int>(); set => Set(value); }

    [Storage("ChestHits", "Chest hits")]
    public int ChestHits { get => Get<int>(); set => Set(value); }

    [Storage("StomachHits", "Stomach hits")]
    public int StomachHits { get => Get<int>(); set => Set(value); }

    [Storage("LeftArmHits", "Left arm hits")]
    public int LeftArmHits { get => Get<int>(); set => Set(value); }

    [Storage("RightArmHits", "Right arm hits")]
    public int RightArmHits { get => Get<int>(); set => Set(value); }

    [Storage("LeftLegHits", "Left leg hits")]
    public int LeftLegHits { get => Get<int>(); set => Set(value); }

    [Storage("RightLegHits", "Right leg hits")]
    public int RightLegHits { get => Get<int>(); set => Set(value); }

    [Storage("NeckHits", "Neck hits")]
    public int NeckHits { get => Get<int>(); set => Set(value); }

    [Storage("GearHits", "Gear hits")]
    public int GearHits { get => Get<int>(); set => Set(value); }

    [Storage("Grenades", "Grenades thrown")]
    public int Grenades { get => Get<int>(); set => Set(value); }

    [Storage("MVP", "MVP awards")]
    public int MVP { get => Get<int>(); set => Set(value); }

    [Storage("RoundWin", "Rounds won")]
    public int RoundWin { get => Get<int>(); set => Set(value); }

    [Storage("RoundLose", "Rounds lost")]
    public int RoundLose { get => Get<int>(); set => Set(value); }

    [Storage("GameWin", "Games won")]
    public int GameWin { get => Get<int>(); set => Set(value); }

    [Storage("GameLose", "Games lost")]
    public int GameLose { get => Get<int>(); set => Set(value); }

    [Storage("RoundsOverall", "Total rounds played")]
    public int RoundsOverall { get => Get<int>(); set => Set(value); }

    [Storage("RoundsCT", "CT rounds played")]
    public int RoundsCT { get => Get<int>(); set => Set(value); }

    [Storage("RoundsT", "T rounds played")]
    public int RoundsT { get => Get<int>(); set => Set(value); }

    [Storage("BombPlanted", "Bombs planted")]
    public int BombPlanted { get => Get<int>(); set => Set(value); }

    [Storage("BombDefused", "Bombs defused")]
    public int BombDefused { get => Get<int>(); set => Set(value); }

    [Storage("HostageRescued", "Hostages rescued")]
    public int HostageRescued { get => Get<int>(); set => Set(value); }

    [Storage("HostageKilled", "Hostages killed")]
    public int HostageKilled { get => Get<int>(); set => Set(value); }

    [Storage("NoScopeKill", "No scope kills")]
    public int NoScopeKill { get => Get<int>(); set => Set(value); }

    [Storage("PenetratedKill", "Penetration kills")]
    public int PenetratedKill { get => Get<int>(); set => Set(value); }

    [Storage("ThruSmokeKill", "Through smoke kills")]
    public int ThruSmokeKill { get => Get<int>(); set => Set(value); }

    [Storage("FlashedKill", "Flashed kills")]
    public int FlashedKill { get => Get<int>(); set => Set(value); }

    [Storage("DominatedKill", "Domination kills")]
    public int DominatedKill { get => Get<int>(); set => Set(value); }

    [Storage("RevengeKill", "Revenge kills")]
    public int RevengeKill { get => Get<int>(); set => Set(value); }

    [Storage("AssistFlash", "Flash assists")]
    public int AssistFlash { get => Get<int>(); set => Set(value); }

    [JsonIgnore]
    public Timer? SpawnMessageTimer { get; set; }

    [JsonIgnore]
    public bool IsSpawned { get; set; } = false;
}
