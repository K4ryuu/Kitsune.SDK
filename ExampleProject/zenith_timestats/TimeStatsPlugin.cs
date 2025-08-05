using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core.Translations;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Core.Attributes.Version;
using CounterStrikeSharp.API.Modules.Commands;
using Kitsune.SDK.Utilities;

using Player = Kitsune.SDK.Core.Base.Player;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace K4_Zenith_TimeStats;

[MinimumApiVersion(300)]
[MinimumSdkVersion(1)]
public sealed partial class TimeStatsPlugin : SdkPlugin, ISdkConfig<TimeStatsConfig>, ISdkStorage<PlayerTimeData>, ISdkSettings<PlayerTimeSettings>
{
    public override string ModuleName => "K4-Zenith | Time Statistics";
    public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleDescription => "Track and display player playtime statistics";

    public new TimeStatsConfig Config => GetTypedConfig<TimeStatsConfig>();

    private readonly Dictionary<CCSPlayerController, PlayerTimeData> _playerTimes = [];
    private Timer? _updateTimer;

    protected override void SdkLoad(bool hotReload)
    {
        ChatProcessor.TryInject(this);
        CenterMessageHandler.TryInject(this);

        RegisterCommands();
        RegisterEvents();

        _updateTimer = AddTimer(10.0f, OnTimerElapsed, TimerFlags.REPEAT);
    }

    protected override void SdkUnload(bool hotReload)
    {
        foreach (var timeData in _playerTimes.Values)
        {
            UpdatePlaytime(timeData);
        }

        _playerTimes.Clear();
        _updateTimer?.Kill();
    }

    private void RegisterCommands()
    {
        Commands.Register(Config.PlaytimeCommands, "Show the playtime informations", OnPlaytimeCommand,
            usage: CommandUsage.CLIENT_ONLY);

        Commands.Register(Config.TodayCommands, "Show today's playtime information", OnTodayCommand,
            usage: CommandUsage.CLIENT_ONLY);
    }

    private void OnTimerElapsed()
    {
        int interval = Config.NotificationInterval;
        if (interval <= 0)
            return;

        foreach (var (controller, timeData) in _playerTimes)
        {
            UpdatePlaytime(timeData);

            bool hasPlaytime = timeData.TotalPlaytime > 1 ||
                             timeData.TerroristPlaytime > 1 ||
                             timeData.CounterTerroristPlaytime > 1 ||
                             timeData.SpectatorPlaytime > 1 ||
                             timeData.AlivePlaytime > 1 ||
                             timeData.DeadPlaytime > 1;

            if (hasPlaytime)
            {
                CheckAndSendNotification(controller, timeData, interval);
            }
        }
    }

    private static void UpdatePlaytime(PlayerTimeData data)
    {
        long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        double sessionDurationMinutes = Math.Round((currentTime - data.LastUpdateTime) / 60.0, 2);

        string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        string lastPlayDate = data.LastPlayDate;

        if (string.IsNullOrEmpty(lastPlayDate) || lastPlayDate != currentDate)
        {
            data.TodayPlaytime = 0.0;
            data.LastPlayDate = currentDate;
        }

        data.TodayPlaytime = Math.Round(data.TodayPlaytime + sessionDurationMinutes, 2);
        data.TotalPlaytime = Math.Round(data.TotalPlaytime + sessionDurationMinutes, 2);

        switch (data.CurrentTeam)
        {
            case CsTeam.Terrorist:
                data.TerroristPlaytime = Math.Round(data.TerroristPlaytime + sessionDurationMinutes, 2);
                break;
            case CsTeam.CounterTerrorist:
                data.CounterTerroristPlaytime = Math.Round(data.CounterTerroristPlaytime + sessionDurationMinutes, 2);
                break;
            default:
                data.SpectatorPlaytime = Math.Round(data.SpectatorPlaytime + sessionDurationMinutes, 2);
                break;
        }

        if (data.IsAlive)
            data.AlivePlaytime = Math.Round(data.AlivePlaytime + sessionDurationMinutes, 2);
        else
            data.DeadPlaytime = Math.Round(data.DeadPlaytime + sessionDurationMinutes, 2);

        data.LastUpdateTime = currentTime;
    }

    private void CheckAndSendNotification(CCSPlayerController controller, PlayerTimeData timeData, int interval)
    {
        var k4Player = Player.Find(controller);
        if (k4Player == null || !k4Player.IsLoaded)
            return;

        var settings = k4Player.Settings<PlayerTimeSettings>();
        if (!settings.ShowPlaytime)
            return;

        long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        long lastNotification = timeData.LastNotification;

        if (currentTime - lastNotification >= interval)
        {
            controller.PrintToChat($" {Localizer["k4.general.prefix"]}{Localizer["timestats.notification", FormatTime(controller, timeData.TotalPlaytime)]}");
            timeData.LastNotification = currentTime;
        }
    }

    private string FormatTime(CCSPlayerController player, double minutes)
    {
        int totalMinutes = (int)Math.Floor(minutes);
        int days = totalMinutes / 1440;
        int hours = totalMinutes % 1440 / 60;
        int mins = totalMinutes % 60;

        return Localizer.ForPlayer(player, "timestats.time.format", days, hours, mins);
    }
}