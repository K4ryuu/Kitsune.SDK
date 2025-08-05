using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Core.Base;
using CounterStrikeSharp.API.Core.Translations;

namespace K4Zenith.Stats;

public partial class StatsPlugin
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

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _firstBlood = false;
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        var storage = player.Storage<PlayerStatsData>();
        if (Config.EnableRequirementMessages)
        {
            int requiredPlayers = Config.MinPlayers;
            if (requiredPlayers > Player.List.Count)
            {
                if (storage.SpawnMessageTimer == null)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {Localizer.ForPlayer(player.Controller, "k4.stats.stats_disabled", requiredPlayers)}");
                    storage.SpawnMessageTimer = AddTimer(3.0f, () => { storage.SpawnMessageTimer = null; });
                }
            }
        }

        storage.IsSpawned = true;
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        bool statsForBots = Config.StatsForBots;

        var victim = @event.Userid;
        var attacker = @event.Attacker;
        var assister = @event.Assister;

        var victimPlayer = Player.Find(victim);
        if (victimPlayer != null && statsForBots || (attacker != null && !attacker.IsBot))
        {
            var storage = victimPlayer?.Storage<PlayerStatsData>();
            if (storage != null)
            {
                storage.Deaths++;
            }
        }

        var attackerPlayer = Player.Find(attacker);
        if (attackerPlayer != null && attacker != victim && (statsForBots || (victim != null && !victim.IsBot)))
        {
            var storage = attackerPlayer?.Storage<PlayerStatsData>();
            if (storage != null)
            {
                storage.Kills++;

                if (IsFirstBlood())
                    storage.FirstBlood++;

                if (@event.Noscope)
                    storage.NoScopeKill++;

                if (@event.Penetrated > 0)
                    storage.PenetratedKill++;

                if (@event.Thrusmoke)
                    storage.ThruSmokeKill++;

                if (@event.Attackerblind)
                    storage.FlashedKill++;

                if (@event.Dominated > 0)
                    storage.DominatedKill++;

                if (@event.Revenge > 0)
                    storage.RevengeKill++;

                if (@event.Headshot)
                    storage.Headshots++;
            }
        }

        var assisterPlayer = Player.Find(assister);
        if (assisterPlayer != null && (statsForBots || (victim != null && !victim.IsBot)))
        {
            var storage = assisterPlayer?.Storage<PlayerStatsData>();
            if (storage != null)
            {
                storage.Assists++;

                if (@event.Assistedflash)
                    storage.AssistFlash++;
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        player.Storage<PlayerStatsData>().Grenades++;
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var victim = Player.Find(@event.Userid);
        var attacker = Player.Find(@event.Attacker);

        if (victim != null)
        {
            victim.Storage<PlayerStatsData>().HitsTaken++;
        }

        if (attacker != null && attacker != victim)
        {
            var storage = attacker.Storage<PlayerStatsData>();
            storage.HitsGiven++;

            HitGroup_t hitHroup = (HitGroup_t)@event.Hitgroup;

            switch (hitHroup)
            {
                case HitGroup_t.HITGROUP_HEAD:
                    storage.HeadHits++;
                    break;
                case HitGroup_t.HITGROUP_CHEST:
                    storage.ChestHits++;
                    break;
                case HitGroup_t.HITGROUP_STOMACH:
                    storage.StomachHits++;
                    break;
                case HitGroup_t.HITGROUP_LEFTARM:
                    storage.LeftArmHits++;
                    break;
                case HitGroup_t.HITGROUP_RIGHTARM:
                    storage.RightArmHits++;
                    break;
                case HitGroup_t.HITGROUP_LEFTLEG:
                    storage.LeftLegHits++;
                    break;
                case HitGroup_t.HITGROUP_RIGHTLEG:
                    storage.RightLegHits++;
                    break;
                case HitGroup_t.HITGROUP_NECK:
                    storage.NeckHits++;
                    break;
                case HitGroup_t.HITGROUP_GEAR:
                    storage.GearHits++;
                    break;
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        player.Storage<PlayerStatsData>().BombPlanted++;
        return HookResult.Continue;
    }

    private HookResult OnHostageRescued(EventHostageRescued @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        player.Storage<PlayerStatsData>().HostageRescued++;
        return HookResult.Continue;
    }

    private HookResult OnHostageKilled(EventHostageKilled @event, GameEventInfo info)
    {
        if (!IsStatsAllowed()) return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        player.Storage<PlayerStatsData>().HostageKilled++;
        return HookResult.Continue;
    }

    private HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    {
        if (!IsStatsAllowed()) return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        player.Storage<PlayerStatsData>().BombDefused++;
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        foreach (var player in Player.ValidLoop())
        {
            CsTeam team = player.Controller.Team;
            if (team <= CsTeam.Spectator)
                continue;

            var storage = player.Storage<PlayerStatsData>();
            if (!storage.IsSpawned)
                continue;

            storage.RoundsOverall++;

            if (team == CsTeam.Terrorist)
                storage.RoundsT++;
            else if (team == CsTeam.CounterTerrorist)
                storage.RoundsCT++;

            if ((int)team == @event.Winner)
                storage.RoundWin++;
            else
                storage.RoundLose++;
        }

        return HookResult.Continue;
    }

    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        if (!@event.Weapon.Contains("knife") && !@event.Weapon.Contains("bayonet"))
        {
            player.Storage<PlayerStatsData>().Shoots++;
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var player = Player.Find(@event.Userid);
        if (player == null)
            return HookResult.Continue;

        player.Storage<PlayerStatsData>().MVP++;
        return HookResult.Continue;
    }

    private HookResult OnCsWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        if (!IsStatsAllowed())
            return HookResult.Continue;

        var players = Player.ValidLoop().ToList();

        if (FFAMode)
        {
            HandleFFAMode(players);
        }
        else
        {
            HandleTeamMode(players);
        }

        return HookResult.Continue;
    }

    private static void HandleFFAMode(List<Player> players)
    {
        Player? winner = null;
        int highestScore = int.MinValue;

        foreach (var player in players)
        {
            int score = player.Controller.Score;
            if (score > highestScore)
            {
                highestScore = score;
                winner = player;
            }
        }

        if (winner != null)
        {
            winner.Storage<PlayerStatsData>().GameWin++;
        }

        foreach (var player in players)
        {
            if (player != winner)
            {
                player.Storage<PlayerStatsData>().GameLose++;
            }
        }
    }

    private static void HandleTeamMode(List<Player> players)
    {
        int ctScore = 0;
        int tScore = 0;

        var teams = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager");
        foreach (var team in teams)
        {
            if (team.Teamname == "CT")
                ctScore = team.Score;
            else if (team.Teamname == "TERRORIST")
                tScore = team.Score;
        }

        CsTeam winnerTeam = ctScore > tScore ? CsTeam.CounterTerrorist :
                            tScore > ctScore ? CsTeam.Terrorist :
                            CsTeam.None;

        if (winnerTeam > CsTeam.Spectator)
        {
            foreach (var player in players)
            {
                if (player.Controller.Team > CsTeam.Spectator)
                {
                    player.Storage<PlayerStatsData>().GameWin++;
                }
            }
        }
    }
}