using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Utilities;
using Microsoft.Extensions.Logging;

namespace K4_Zenith_Ranks;

public sealed partial class RanksPlugin
{
    private void OnRankCommand(CCSPlayerController? controller, CommandInfo info)
    {
        if (controller == null)
            return;

        var player = Player.Find(controller);

        // Check if player data is loaded
        if (player == null || !player.IsLoaded)
        {
            info.ReplyToCommand($" {Localizer.ForPlayer(controller, "k4.general.prefix")} {Localizer.ForPlayer(controller, "k4.general.loading")}");
            return;
        }

        var data = player.Storage<PlayerRankData>();

        var (currentRank, nextRank) = GetRanksByPoints(data.Points);

        long pointsToNextRank = nextRank != null ? nextRank.Point - data.Points : 0;

        info.ReplyToCommand(Localizer.ForPlayer(controller, "k4.phrases.rank.title", controller.PlayerName));
        info.ReplyToCommand(Localizer.ForPlayer(controller, "k4.phrases.rank.line1", currentRank?.ChatColor ?? ChatColors.Grey.ToString(), currentRank?.Name ?? Localizer.ForPlayer(controller, "k4.phrases.rank.none"), $"{data.Points:N0}"));

        if (nextRank != null)
        {
            info.ReplyToCommand(Localizer.ForPlayer(controller, "k4.phrases.rank.line2", nextRank.ChatColor, nextRank.Name, $"{pointsToNextRank:N0}"));
        }
    }

    private void OnGivePoints(CCSPlayerController? controller, CommandInfo info)
    {
        ProcessPointsCommand(controller, info, PointsAction.Give);
    }

    private void OnTakePoints(CCSPlayerController? controller, CommandInfo info)
    {
        ProcessPointsCommand(controller, info, PointsAction.Take);
    }

    private void OnSetPoints(CCSPlayerController? controller, CommandInfo info)
    {
        ProcessPointsCommand(controller, info, PointsAction.Set);
    }

    private void OnResetPoints(CCSPlayerController? controller, CommandInfo info)
    {
        ProcessPointsCommand(controller, info, PointsAction.Reset);
    }

    private void ProcessPointsCommand(CCSPlayerController? controller, CommandInfo info, PointsAction action)
    {
        // Handle offline player by SteamID
        if (ulong.TryParse(info.GetArg(1), out ulong steamId))
        {
            var onlinePlayer = Utilities.GetPlayerFromSteamId(steamId);
            if (onlinePlayer == null)
            {
                ProcessOfflinePlayer(controller, steamId, action, info);
                return;
            }
        }

        // Process online targets
        TargetResult targets = info.GetArgTargetResult(1);
        if (!targets.Any())
        {
            info.ReplyToCommand($" {ChatColors.Red}{Localizer.ForPlayer(controller, "k4.phrases.no-target")}");
            return;
        }

        long amount = 0;
        if (action != PointsAction.Reset)
        {
            if (!long.TryParse(info.GetArg(2), out amount) || amount <= 0)
            {
                info.ReplyToCommand($" {ChatColors.Red}{Localizer.ForPlayer(controller, "k4.phrases.invalid-amount")}");
                return;
            }
        }

        foreach (var target in targets)
        {
            var player = Player.Find(target);
            if (player == null)
            {
                info.ReplyToCommand($" {ChatColors.Red}{Localizer.ForPlayer(controller, "k4.phrases.cant-target", target.PlayerName)}");
                continue;
            }

            ProcessPlayerPoints(controller, player, action, amount);
        }
    }

    private void ProcessPlayerPoints(CCSPlayerController? admin, Player player, PointsAction action, long amount)
    {
        var data = player.Storage<PlayerRankData>();
        long oldPoints = data.Points;
        string adminName = admin?.PlayerName ?? "CONSOLE";

        switch (action)
        {
            case PointsAction.Give:
                data.Points += amount;
                ClampPoints(data);
                player.PrintToChat(Localizer.ForPlayer(player.Controller, "k4.phrases.points-given", adminName, amount));
                Logger.LogWarning("{Admin} ({AdminId}) gave {Target} ({TargetId}) {Amount} rank points",
                    adminName, admin?.SteamID ?? 0, player.Controller.PlayerName, player.Controller.SteamID, amount);
                break;

            case PointsAction.Take:
                data.Points = Math.Max(0, data.Points - amount);
                player.PrintToChat(Localizer.ForPlayer(player.Controller, "k4.phrases.points-taken", adminName, amount));
                Logger.LogWarning("{Admin} ({AdminId}) took {Amount} rank points from {Target} ({TargetId})",
                    adminName, admin?.SteamID ?? 0, amount, player.Controller.PlayerName, player.Controller.SteamID);
                break;

            case PointsAction.Set:
                data.Points = amount;
                ClampPoints(data);
                player.PrintToChat(Localizer.ForPlayer(player.Controller, "k4.phrases.points-set", adminName, amount));
                Logger.LogWarning("{Admin} ({AdminId}) set {Target} ({TargetId}) rank points to {Amount}",
                    adminName, admin?.SteamID ?? 0, player.Controller.PlayerName, player.Controller.SteamID, amount);
                break;

            case PointsAction.Reset:
                data.Points = Config.Points.StartPoints;
                player.PrintToChat(Localizer.ForPlayer(player.Controller, "k4.phrases.points-reset", adminName));
                Logger.LogWarning("{Admin} ({AdminId}) reset {Target} ({TargetId}) rank points",
                    adminName, admin?.SteamID ?? 0, player.Controller.PlayerName, player.Controller.SteamID);
                break;
        }

        // Update rank if points changed
        if (data.Points != oldPoints)
        {
            UpdatePlayerRank(player, data.Points, oldPoints);
        }
    }

    private void ProcessOfflinePlayer(CCSPlayerController? admin, ulong steamId, PointsAction action, CommandInfo info)
    {
        string adminName = admin?.PlayerName ?? "CONSOLE";

        CSSThread.RunOnMainThread(async () =>
        {
            try
            {
                // Get current points using SDK method
                long points = await Storage.GetStorageValueAsync<long>(steamId, "points");
                if (points == 0)
                    points = Config.Points.StartPoints;

                // Calculate new points based on action
                long amount = 0;
                if (action != PointsAction.Reset)
                {
                    if (!long.TryParse(info.GetArg(2), out amount) || amount <= 0)
                    {
                        info.ReplyToCommand($" {ChatColors.Red}Invalid amount.");
                        return;
                    }
                }

                switch (action)
                {
                    case PointsAction.Give:
                        points += amount;
                        points = ClampPointsValue(points);
                        break;

                    case PointsAction.Take:
                        points = Math.Max(0, points - amount);
                        break;

                    case PointsAction.Set:
                        points = ClampPointsValue(amount);
                        break;

                    case PointsAction.Reset:
                        points = Config.Points.StartPoints;
                        break;
                }

                // Determine rank
                var (rank, _) = GetRanksByPoints(points);

                // Save using SDK methods
                await Storage.SetStorageValueAsync(steamId, "points", points, saveImmediately: true);
                await Storage.SetStorageValueAsync(steamId, "rank", rank?.Name ?? "k4.phrases.rank.none", saveImmediately: true);

                info.ReplyToCommand($" {ChatColors.Green}Successfully updated offline player data.");
                Logger.LogWarning("{Admin} ({AdminId}) modified offline player {SteamId} - Action: {Action}, Amount: {Amount}",
                    adminName, admin?.SteamID ?? 0, steamId, action, amount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to update offline player data");
                info.ReplyToCommand($" {ChatColors.Red}Failed to update offline player data.");
            }
        });
    }

    private enum PointsAction
    {
        Give,
        Take,
        Set,
        Reset
    }

    private void ClampPoints(PlayerRankData data)
    {
        if (Config.Points.MaxPoints > 0 && data.Points > Config.Points.MaxPoints)
            data.Points = Config.Points.MaxPoints;
    }

    private long ClampPointsValue(long points)
    {
        return Config.Points.MaxPoints > 0 && points > Config.Points.MaxPoints ? Config.Points.MaxPoints : points;
    }
}