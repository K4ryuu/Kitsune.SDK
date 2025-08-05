using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Config;
using Kitsune.SDK.Core.Attributes.Data;
using Kitsune.SDK.Services;

namespace K4_Zenith_TimeStats;

public class TimeStatsConfig
{
    [Config("CenterMenuMode", "Whether to use center HTML messages instead of chat")]
    public bool CenterMenuMode { get; set; } = true;

    [Config("CenterMessageTime", "Time in seconds to display center messages")]
    public int CenterMessageTime { get; set; } = 5;

    [Config("PlaytimeCommands", "List of commands that shows player time statistics")]
    public List<string> PlaytimeCommands { get; set; } = ["playtime", "mytime"];

    [Config("TodayCommands", "List of commands that shows today's playtime statistics")]
    public List<string> TodayCommands { get; set; } = ["today", "mytoday"];

    [Config("NotificationInterval", "Interval in seconds between playtime notifications")]
    public int NotificationInterval { get; set; } = 300;
}

public class PlayerTimeData : StorageBase
{
    [Storage("TotalPlaytime", "Total playtime in minutes", track: true)]
    public double TotalPlaytime { get => Get<double>(); set => Set(value); }

    [Storage("TerroristPlaytime", "Terrorist team playtime in minutes", track: true)]
    public double TerroristPlaytime { get => Get<double>(); set => Set(value); }

    [Storage("CounterTerroristPlaytime", "Counter-Terrorist team playtime in minutes", track: true)]
    public double CounterTerroristPlaytime { get => Get<double>(); set => Set(value); }

    [Storage("SpectatorPlaytime", "Spectator playtime in minutes", track: true)]
    public double SpectatorPlaytime { get => Get<double>(); set => Set(value); }

    [Storage("AlivePlaytime", "Time spent alive in minutes", track: true)]
    public double AlivePlaytime { get => Get<double>(); set => Set(value); }

    [Storage("DeadPlaytime", "Time spent dead in minutes", track: true)]
    public double DeadPlaytime { get => Get<double>(); set => Set(value); }

    [Storage("LastNotification", "Unix timestamp of last notification")]
    public long LastNotification { get => Get<long>(); set => Set(value); }

    [Storage("LastPlayDate", "Last play date in yyyy-MM-dd format")]
    public string LastPlayDate { get => Get<string>(); set => Set(value); }

    [Storage("TodayPlaytime", "Today's playtime in minutes", track: true)]
    public double TodayPlaytime { get => Get<double>(); set => Set(value); }

    [JsonIgnore]
    public long LastUpdateTime { get; set; }

    [JsonIgnore]
    public CsTeam CurrentTeam { get; set; } = CsTeam.None;

    [JsonIgnore]
    public bool IsAlive { get; set; }
}

public class PlayerTimeSettings : SettingsBase
{
    [Setting("ShowPlaytime", "Show playtime notifications")]
    public bool ShowPlaytime { get => Get<bool>(); set => Set(value); }
}