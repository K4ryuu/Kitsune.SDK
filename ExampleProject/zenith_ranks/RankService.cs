using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Player;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Services;

namespace K4_Zenith_Ranks;

public sealed partial class RanksPlugin
{
    public void ModifyPlayerPoints(Player player, int points, string translationKey, string? eventInfo = null)
    {
        if (!ShouldProcessEvent() || points == 0)
            return;

        var data = player.Storage<PlayerRankData>();

        // Apply modifiers and limits
        points = ApplyVipMultiplier(player, points);
        long oldPoints = data.Points;
        data.Points = ApplyPointsWithLimits(data.Points, points);

        // Handle UI updates
        HandleScoreboardUpdate(player, data);
        HandlePointMessages(player, points, translationKey, eventInfo, data);

        // Check for rank change
        UpdatePlayerRank(player, data.Points, oldPoints);
    }

    private int ApplyVipMultiplier(Player player, int points)
    {
        if (points <= 0 || Config.Settings.VipMultiplier <= 1.0)
            return points;

        var vipFlags = Config.Settings.VipFlags;
        if (vipFlags.Count > 0 && vipFlags.Any(flag => AdminManager.PlayerHasPermissions(player.Controller, flag)))
        {
            return (int)(points * Config.Settings.VipMultiplier);
        }

        return points;
    }

    private long ApplyPointsWithLimits(long currentPoints, int pointsToAdd)
    {
        long newPoints = currentPoints + pointsToAdd;
        newPoints = Math.Max(0, newPoints);

        if (Config.Points.MaxPoints > 0 && newPoints > Config.Points.MaxPoints)
            newPoints = Config.Points.MaxPoints;

        return newPoints;
    }

    private void HandleScoreboardUpdate(Player player, PlayerRankData data)
    {
        if (Config.Settings.ScoreboardScoreSync)
        {
            player.Controller.Score = (int)data.Points;
        }
    }

    private void HandlePointMessages(Player player, int points, string translationKey, string? eventInfo, PlayerRankData data)
    {
        if (Config.Settings.PointSummaries)
        {
            data.RoundPoints += points;
        }
        else
        {
            // Show point change message
            ShowPointChangeMessage(player, points, translationKey, eventInfo);
        }
    }

    public void UpdatePlayerRank(Player player, long currentPoints, long? oldPoints = null)
    {
        var (oldRank, _) = oldPoints.HasValue ? GetRanksByPoints(oldPoints.Value) : (null, null);
        var (newRank, _) = GetRanksByPoints(currentPoints);
        var data = player.Storage<PlayerRankData>();

        data.Rank = newRank?.Name ?? "k4.phrases.rank.none";

        // Handle rank change notification
        if (ShouldShowRankChange(player, oldRank, newRank, oldPoints))
        {
            ShowRankChangeMessage(player, oldRank!, newRank!);
        }

        // Update scoreboard rank
        if (Config.Settings.UseScoreboardRanks)
        {
            SetCompetitiveRank(player, currentPoints);
        }
    }

    private bool ShouldShowRankChange(Player player, Rank? oldRank, Rank? newRank, long? oldPoints)
    {
        return oldRank?.Id != newRank?.Id
            && oldPoints.HasValue
            && player.Settings<PlayerSettings>().ShowRankChanges
            && Config.Settings.ShowRankChanges
            && newRank != null
            && oldRank != null;
    }

    private void ShowRankChangeMessage(Player player, Rank oldRank, Rank newRank)
    {
        bool isPromotion = newRank.Id > oldRank.Id;
        string translationKey = isPromotion ? "k4.phrases.rank-up" : "k4.phrases.rank-down";
        string message = Localizer.ForPlayer(player.Controller, translationKey, newRank.Name);

        player.PrintToCenter(message, 5, ActionPriority.Normal);
    }

    public (Rank? current, Rank? next) GetRanksByPoints(long points)
    {
        // Binary search for current rank (highest rank <= points)
        int left = 0, right = Ranks.Count - 1;
        Rank? current = null, next = null;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (Ranks[mid].Point <= points)
            {
                current = Ranks[mid];
                // Check if there's a next rank
                if (mid + 1 < Ranks.Count)
                {
                    next = Ranks[mid + 1];
                }
                left = mid + 1;
            }
            else
            {
                next = Ranks[mid];
                right = mid - 1;
            }
        }

        return (current, next);
    }


    public int CalculateDynamicPoints(Player attacker, Player victim, int basePoints)
    {
        if (!Config.Settings.DynamicDeathPoints)
            return basePoints;

        var attackerData = attacker.Storage<PlayerRankData>();
        var victimData = victim.Storage<PlayerRankData>();

        long pointDiff = victimData.Points - attackerData.Points;
        double multiplier = 1.0 + (pointDiff / 10000.0);

        // Clamp multiplier to configured limits
        multiplier = Math.Clamp(multiplier, Config.Settings.DynamicDeathPointsMinMultiplier, Config.Settings.DynamicDeathPointsMaxMultiplier);

        return (int)(basePoints * multiplier);
    }

    public void ShowRoundSummary(Player player)
    {
        var data = player.Storage<PlayerRankData>();
        if (data.RoundPoints == 0)
            return;

        string message = data.RoundPoints > 0
            ? Localizer.ForPlayer(player.Controller, "k4.phrases.round-summary-earn", data.RoundPoints, data.Points)
            : Localizer.ForPlayer(player.Controller, "k4.phrases.round-summary-lose", Math.Abs(data.RoundPoints), data.Points);

        player.PrintToChat(message);
        data.RoundPoints = 0;
    }

    private void ShowPointChangeMessage(Player player, int points, string eventKey, string? eventInfo)
    {
        var data = player.Storage<PlayerRankData>();
        long newPoints = data.Points;

        string message = Localizer.ForPlayer(player.Controller, points >= 0 ? "k4.phrases.gain" : "k4.phrases.loss",
            $"{newPoints:N0}", Math.Abs(points), eventInfo ?? Localizer.ForPlayer(player.Controller, eventKey));

        player.PrintToChat($" {Localizer.ForPlayer(player.Controller, "k4.general.prefix")} {message}");
    }

    private static readonly Dictionary<int, (sbyte rankType, Func<Rank, int, int> rankCalculator)> ScoreboardModes = new()
    {
        [1] = (11, (rank, points) => points),                   // Premier
        [2] = (12, (rank, points) => Math.Min(rank.Id, 18)),    // Competitive
        [3] = (7, (rank, points) => Math.Min(rank.Id, 18)),     // Wingman
        [4] = (10, (rank, points) => Math.Min(rank.Id, 15))     // Danger Zone
    };

    private void SetCompetitiveRank(Player player, long currentPoints)
    {
        var (rank, _) = GetRanksByPoints(currentPoints);
        if (rank == null) return;

        int mode = Config.Settings.ScoreboardMode;
        player.Controller.CompetitiveWins = 10;

        if (ScoreboardModes.TryGetValue(mode, out var modeConfig))
        {
            player.Controller.CompetitiveRanking = modeConfig.rankCalculator(rank, (int)currentPoints);
            player.Controller.CompetitiveRankType = modeConfig.rankType;
        }
        else
        {
            // Custom mode
            var settings = Config.Settings;
            int customRank = rank.Id > settings.RankMax
                ? settings.RankBase + settings.RankMax - settings.RankMargin
                : settings.RankBase + (rank.Id - settings.RankMargin - 1);

            player.Controller.CompetitiveRanking = customRank;
            player.Controller.CompetitiveRankType = 12;
        }
    }
}