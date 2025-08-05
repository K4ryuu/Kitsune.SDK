using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Extensions.Player;

namespace K4_Zenith_Ranks;

public sealed partial class RanksPlugin
{
    private Dictionary<int, int> _killStreakPoints = new();

    private void InitializeKillStreakPoints()
    {
        _killStreakPoints = new Dictionary<int, int>
        {
            { 2, Config.Points.DoubleKill },
            { 3, Config.Points.TripleKill },
            { 4, Config.Points.Domination },
            { 5, Config.Points.Rampage },
            { 6, Config.Points.MegaKill },
            { 7, Config.Points.Ownage },
            { 8, Config.Points.UltraKill },
            { 9, Config.Points.KillingSpree },
            { 10, Config.Points.MonsterKill },
            { 11, Config.Points.Unstoppable },
            { 12, Config.Points.GodLike }
        };
    }

    public void HandlePlayerDeath(EventPlayerDeath @event)
    {
        if (@event.Userid == null) return;

        var victim = Player.Find(@event.Userid);
        var attacker = Player.Find(@event.Attacker);
        var assister = Player.Find(@event.Assister);

        // Handle victim death
        if (victim != null)
        {
            HandleVictimDeath(victim, attacker, @event);
        }

        // Handle attacker kill
        if (attacker != null && attacker != victim)
        {
            HandleAttackerKill(attacker, victim, @event);
        }

        // Handle assist
        if (assister != null && assister != victim)
        {
            HandleAssist(assister, victim, @event);
        }
    }

    private void HandleVictimDeath(Player victim, Player? attacker, EventPlayerDeath @event)
    {
        // Suicide or world damage
        if (attacker == null || attacker == victim)
        {
            if (!_isGameEnd)
            {
                ModifyPlayerPoints(victim, Config.Points.Suicide, "k4.events.suicide");
            }
        }
        else
        {
            // Check if we should process bot deaths
            if (!Config.Settings.PointsForBots && @event.Attacker?.IsBot == true)
                return;

            // Build extended death message if enabled
            string? eventInfo = null;
            if (attacker != null && Config.Settings.ExtendedDeathMessages)
            {
                var attackerData = attacker.Storage<PlayerRankData>();
                eventInfo = Localizer.ForPlayer(victim.Controller, "k4.phrases.death-extended", attacker.Controller.PlayerName, $"{attackerData.Points:N0}");
            }

            // Calculate points
            int points = Config.Points.Death;
            if (Config.Settings.DynamicDeathPoints && attacker != null)
            {
                points = CalculateDynamicPoints(attacker, victim, points);
            }

            ModifyPlayerPoints(victim, points, "k4.events.playerdeath", eventInfo);
        }

        // Reset kill streak
        var victimData = victim.Storage<PlayerRankData>();
        victimData.KillStreak.Reset();
    }

    private void HandleAttackerKill(Player attacker, Player? victim, EventPlayerDeath @event)
    {
        // Check bot kill settings
        if (!Config.Settings.PointsForBots && @event.Userid?.IsBot == true)
            return;

        // Check team kill
        if (!FFAMode && attacker.Controller.Team == @event.Userid?.Team)
        {
            ModifyPlayerPoints(attacker, Config.Points.TeamKill, "k4.events.teamkill");
        }
        else
        {
            HandleNormalKill(attacker, victim, @event);
        }
    }

    private void HandleNormalKill(Player attacker, Player? victim, EventPlayerDeath @event)
    {
        // Build extended kill message if enabled
        string? eventInfo = null;
        if (victim != null && Config.Settings.ExtendedDeathMessages)
        {
            var victimData = victim.Storage<PlayerRankData>();
            eventInfo = Localizer.ForPlayer(attacker.Controller, "k4.phrases.kill-extended", victim.Controller.PlayerName, $"{victimData.Points:N0}");
        }

        // Calculate base kill points
        int points = Config.Points.Kill;
        if (Config.Settings.DynamicDeathPoints && victim != null)
        {
            points = CalculateDynamicPoints(attacker, victim, points);
        }

        ModifyPlayerPoints(attacker, points, "k4.events.kill", eventInfo);

        // Handle special kill bonuses
        HandleKillBonuses(attacker, @event);

        // Handle kill streak
        HandleKillStreak(attacker);
    }

    private void HandleKillBonuses(Player attacker, EventPlayerDeath @event)
    {
        // Headshot
        if (@event.Headshot)
            ModifyPlayerPoints(attacker, Config.Points.Headshot, "k4.events.headshot");

        // Penetration
        if (@event.Penetrated > 0)
            ModifyPlayerPoints(attacker, Config.Points.Penetrated * @event.Penetrated, "k4.events.penetrated");

        // No scope
        if (@event.Noscope)
            ModifyPlayerPoints(attacker, Config.Points.NoScope, "k4.events.noscope");

        // Through smoke
        if (@event.Thrusmoke)
            ModifyPlayerPoints(attacker, Config.Points.Thrusmoke, "k4.events.thrusmoke");

        // Blind kill
        if (@event.Attackerblind)
            ModifyPlayerPoints(attacker, Config.Points.BlindKill, "k4.events.blindkill");

        // Long distance
        if (@event.Distance >= Config.Points.LongDistance)
            ModifyPlayerPoints(attacker, Config.Points.LongDistanceKill, "k4.events.longdistance");

        // Weapon specific bonuses
        HandleWeaponKillBonus(attacker, @event.Weapon);
    }

    private void HandleWeaponKillBonus(Player attacker, string weapon)
    {
        string weaponLower = weapon.ToLower();

        switch (weaponLower)
        {
            case string w when w.Contains("hegrenade"):
                ModifyPlayerPoints(attacker, Config.Points.GrenadeKill, "k4.events.grenadekill");
                break;
            case string w when w.Contains("inferno"):
                ModifyPlayerPoints(attacker, Config.Points.InfernoKill, "k4.events.infernokill");
                break;
            case string w when w.Contains("knife") || w.Contains("bayonet"):
                ModifyPlayerPoints(attacker, Config.Points.KnifeKill, "k4.events.knifekill");
                break;
            case "taser":
                ModifyPlayerPoints(attacker, Config.Points.TaserKill, "k4.events.taserkill");
                break;
            case string w when w.Contains("grenade") || w.Contains("molotov") || w.Contains("flashbang") || w.Contains("bumpmine"):
                ModifyPlayerPoints(attacker, Config.Points.ImpactKill, "k4.events.impactkill");
                break;
        }
    }

    private void HandleKillStreak(Player attacker)
    {
        var data = attacker.Storage<PlayerRankData>();
        long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();

        // Check if kill streak continues
        bool isValidStreak = Config.Points.SecondsBetweenKills <= 0 ||
                            (currentTime - data.KillStreak.LastKillTime <= Config.Points.SecondsBetweenKills);

        if (isValidStreak)
        {
            data.KillStreak.KillCount++;
            data.KillStreak.LastKillTime = currentTime;

            // Award kill streak bonus
            if (_killStreakPoints.TryGetValue(data.KillStreak.KillCount, out int streakPoints) && streakPoints != 0)
            {
                ModifyPlayerPoints(attacker, streakPoints, $"k4.events.killstreak{data.KillStreak.KillCount}");
            }
        }
        else
        {
            // Reset streak and start new one
            data.KillStreak.KillCount = 1;
            data.KillStreak.LastKillTime = currentTime;
        }
    }

    private void HandleAssist(Player assister, Player? victim, EventPlayerDeath @event)
    {
        // Check bot assist settings
        if (!Config.Settings.PointsForBots && @event.Userid?.IsBot == true)
            return;

        // Check team assist
        bool isTeamAssist = !FFAMode && assister.Controller.Team == @event.Userid?.Team;

        if (isTeamAssist)
        {
            ModifyPlayerPoints(assister, Config.Points.TeamKillAssist, "k4.events.teamkillassist");

            if (@event.Assistedflash)
            {
                ModifyPlayerPoints(assister, Config.Points.TeamKillAssistFlash, "k4.events.teamkillassistflash");
            }
        }
        else
        {
            // Build extended assist message if enabled
            string? eventInfo = null;
            if (victim != null && Config.Settings.ExtendedDeathMessages)
            {
                var victimData = victim.Storage<PlayerRankData>();
                eventInfo = Localizer.ForPlayer(assister.Controller, "k4.phrases.assist-extended",
                    victim.Controller.PlayerName, $"{victimData.Points:N0}");
            }

            ModifyPlayerPoints(assister, Config.Points.Assist, "k4.events.assist", eventInfo);

            if (@event.Assistedflash)
            {
                ModifyPlayerPoints(assister, Config.Points.AssistFlash, "k4.events.assistflash");
            }
        }
    }
}