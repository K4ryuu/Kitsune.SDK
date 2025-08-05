using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Extensions.Player;

namespace K4Zenith.Stats;

public partial class StatsPlugin
{
    private void RegisterCommands()
    {
        Commands.Register(Config.StatisticCommands, "Show your statistics", OnStatsCommand, usage: CommandUsage.CLIENT_ONLY);
    }

    private void OnStatsCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        var customPlayer = Player.Find(player);
        if (customPlayer == null) return;

        var statsData = customPlayer.Storage<PlayerStatsData>();
        ShowStatsMenu(player, statsData);
    }
}