using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Extensions.Player;

namespace K4_Zenith_Ranks;

public partial class RanksPlugin
{
    private HookResult OnPlayerActivate(EventPlayerActivate @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        Player.GetOrCreate<Player>(player);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        Player.Find(@event.Userid)?.Dispose();
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        // Show requirement message if needed
        if (Config.Settings.EnableRequirementMessages && Player.List.Count < Config.Settings.MinPlayers)
        {
            player.PrintToChat(Localizer.ForPlayer(@event.Userid, "k4.phrases.points_disabled", Config.Settings.MinPlayers));
        }

        player.Storage<PlayerRankData>().HasSpawned = true;
        return HookResult.Continue;
    }

    // Round events
    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!ShouldProcessEvent())
            return HookResult.Continue;

        foreach (var player in Player.List.Values)
        {
            var storage = player.Storage<PlayerRankData>();

            // Reset kill streaks if configured
            if (Config.Points.RoundEndKillStreakReset)
            {
                storage.KillStreak.Reset();
            }

            // Only process points for players who spawned
            if (storage.HasSpawned && player.Controller.Team > CsTeam.Spectator)
            {
                // Give round win/lose points
                if (player.Controller.TeamNum == @event.Winner)
                {
                    ModifyPlayerPoints(player, Config.Points.RoundWin, "k4.events.roundwin");
                }
                else
                {
                    ModifyPlayerPoints(player, Config.Points.RoundLose, "k4.events.roundlose");
                }

                // Show round summary if enabled
                if (Config.Settings.PointSummaries)
                {
                    if (player.Settings<PlayerSettings>().ShowRankChanges)
                    {
                        ShowRoundSummary(player);
                    }
                }
            }

            // Reset round data
            storage.RoundPoints = 0;
            storage.HasSpawned = false;
        }

        return HookResult.Continue;
    }

    private HookResult OnCsWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        _isGameEnd = true;
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!ShouldProcessEvent())
            return HookResult.Continue;

        HandlePlayerDeath(@event);
        return HookResult.Continue;
    }

    // MVP event
    private HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.MVP, "k4.events.roundmvp");
    }

    // Bomb events
    private HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.BombPlant, "k4.events.bombplanted");
    }

    private HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.BombDefused, "k4.events.bombdefused");
    }

    private HookResult OnBombExploded(EventBombExploded @event, GameEventInfo info)
    {
        if (!ShouldProcessEvent())
            return HookResult.Continue;

        // Give points to all terrorists
        foreach (var player in Player.ValidLoop())
        {
            if (player.Controller.Team == CsTeam.Terrorist && player.Storage<PlayerRankData>().HasSpawned == true)
            {
                ModifyPlayerPoints(player, Config.Points.BombExploded, "k4.events.bombexploded");
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnBombPickup(EventBombPickup @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.BombPickup, "k4.events.bombpickup");
    }

    private HookResult OnBombDropped(EventBombDropped @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.BombDrop, "k4.events.bombdropped");
    }

    // Hostage events
    private HookResult OnHostageRescued(EventHostageRescued @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.HostageRescue, "k4.events.hostagerescued");
    }

    private HookResult OnHostageRescuedAll(EventHostageRescuedAll @event, GameEventInfo info)
    {
        if (!ShouldProcessEvent())
            return HookResult.Continue;

        // Give points to all CTs
        foreach (var player in Player.ValidLoop())
        {
            if (player.Controller.Team == CsTeam.CounterTerrorist && player.Storage<PlayerRankData>().HasSpawned == true)
            {
                ModifyPlayerPoints(player, Config.Points.HostageRescueAll, "k4.events.hostagerescuedall");
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnHostageKilled(EventHostageKilled @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.HostageKill, "k4.events.hostagekilled");
    }

    private HookResult OnHostageHurt(EventHostageHurt @event, GameEventInfo info)
    {
        return ProcessSimplePointEvent(@event.Userid, Config.Points.HostageHurt, "k4.events.hostagehurt");
    }

    private void UpdateScoreboards()
    {
        if (!Config.Settings.UseScoreboardRanks || GameRules == null)
            return;

        foreach (var player in Player.ValidLoop())
        {
            UpdatePlayerRank(player, player.Storage<PlayerRankData>().Points);
        }
    }

    private HookResult ProcessSimplePointEvent(CCSPlayerController? userid, int points, string translationKey)
    {
        if (!ShouldProcessEvent())
            return HookResult.Continue;

        var player = Player.Find(userid);
        if (player != null)
        {
            ModifyPlayerPoints(player, points, translationKey);
        }

        return HookResult.Continue;
    }
}