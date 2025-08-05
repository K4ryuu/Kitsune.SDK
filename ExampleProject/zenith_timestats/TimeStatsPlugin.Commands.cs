using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using Kitsune.SDK.Core.Models.Player;

using Player = Kitsune.SDK.Core.Base.Player;

namespace K4_Zenith_TimeStats;

public sealed partial class TimeStatsPlugin
{
    private void OnPlaytimeCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null)
            return;

        if (_playerTimes.TryGetValue(player, out var timeData))
        {
            UpdatePlaytime(timeData);
            SendDetailedPlaytimeStats(player, timeData);
        }
    }

    private void OnTodayCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null)
            return;

        if (_playerTimes.TryGetValue(player, out var timeData))
        {
            UpdatePlaytime(timeData);
            SendTodayPlaytimeStats(player, timeData);
        }
    }

    private void SendDetailedPlaytimeStats(CCSPlayerController player, PlayerTimeData timeData)
    {
        if (Config.CenterMenuMode)
        {
            string htmlMessage = $@"
                <font color='#ff3333' class='fontSize-m'>{Localizer.ForPlayer(player, "timestats.center.title")}</font><br>
                <font color='#FF6666' class='fontSize-sm'>{Localizer.ForPlayer(player, "timestats.center.total.label")}</font> <font color='#FFFFFF' class='fontSize-s'>{FormatTime(player, timeData.TotalPlaytime)}</font><br>
                <font color='#FF6666' class='fontSize-sm'>{Localizer.ForPlayer(player, "timestats.center.teams.label")}</font> <font color='#FFFFFF' class='fontSize-s'>{Localizer.ForPlayer(player, "timestats.center.teams.value", FormatTime(player, timeData.TerroristPlaytime), FormatTime(player, timeData.CounterTerroristPlaytime))}</font><br>
                <font color='#FF6666' class='fontSize-sm'>{Localizer.ForPlayer(player, "timestats.center.spectator.label")}</font> <font color='#FFFFFF' class='fontSize-s'>{FormatTime(player, timeData.SpectatorPlaytime)}</font><br>
                <font color='#FF6666' class='fontSize-sm'>{Localizer.ForPlayer(player, "timestats.center.status.label")}</font> <font color='#FFFFFF' class='fontSize-s'>{Localizer.ForPlayer(player, "timestats.center.status.value", FormatTime(player, timeData.AlivePlaytime), FormatTime(player, timeData.DeadPlaytime))}</font>";

            Player.Find(player)?.PrintToCenter(htmlMessage, Config.CenterMessageTime, ActionPriority.Low);
        }
        else
        {
            player.PrintToChat($" {Localizer["k4.general.prefix"]}{Localizer["timestats.chat.title", player.PlayerName]}");
            player.PrintToChat(Localizer["timestats.chat.total", FormatTime(player, timeData.TotalPlaytime)]);
            player.PrintToChat(Localizer["timestats.chat.teams", FormatTime(player, timeData.TerroristPlaytime), FormatTime(player, timeData.CounterTerroristPlaytime)]);
            player.PrintToChat(Localizer["timestats.chat.spectator", FormatTime(player, timeData.SpectatorPlaytime)]);
            player.PrintToChat(Localizer["timestats.chat.status", FormatTime(player, timeData.AlivePlaytime), FormatTime(player, timeData.DeadPlaytime)]);
        }
    }

    private void SendTodayPlaytimeStats(CCSPlayerController player, PlayerTimeData timeData)
    {
        if (Config.CenterMenuMode)
        {
            string htmlMessage = $@"
                <font color='#ff3333' class='fontSize-m'>{Localizer.ForPlayer(player, "timestats.today.title")}</font><br>
                <font color='#FFFFFF' class='fontSize-sm'>{FormatTime(player, timeData.TodayPlaytime)}</font>";

            Player.Find(player)?.PrintToCenter(htmlMessage, Config.CenterMessageTime, ActionPriority.Low);
        }
        else
        {
            player.PrintToChat($" {Localizer["k4.general.prefix"]}{Localizer["timestats.today.chat.title", player.PlayerName]}");
            player.PrintToChat(Localizer["timestats.today.chat.playtime", FormatTime(player, timeData.TodayPlaytime)]);
        }
    }
}