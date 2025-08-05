using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Utilities;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Extensions.Player;

namespace K4_Zenith_Ranks;

[MinimumApiVersion(300)]
[MinimumSdkVersion(1)]
public sealed partial class RanksPlugin : SdkPlugin, ISdkConfig<RanksConfig>, ISdkStorage<PlayerRankData>, ISdkSettings<PlayerSettings>
{
    public override string ModuleName => "K4-Zenith | Ranks";
    public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleDescription => "Advanced ranking system with points and achievements";

    // Type-safe config access through ISdkConfig<RanksConfig>
    public new RanksConfig Config => GetTypedConfig<RanksConfig>();

    // State
    private DateTime _lastPlaytimeCheck = DateTime.Now;
    internal bool _isGameEnd;
    private bool _shouldProcessEvents = true;

    // Game references
    public CCSGameRules? GameRules { get; private set; }
    public List<Rank> Ranks { get; private set; } = [];

    public bool FFAMode = false;

    protected override void SdkLoad(bool hotReload)
    {
        ChatProcessor.TryInject(this);
        CenterMessageHandler.TryInject(this);

        // Initialize variables
        InitializeKillStreakPoints();

        // Load ranks configuration
        LoadRanksConfig();

        // Register all components
        RegisterCommands();
        RegisterPlaceholders();
        RegisterEvents();
        SetupTimers();

        // Handle hot reload
        if (hotReload)
        {
            GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            FFAMode = ConVar.Find("mp_teammates_are_enemies")?.GetPrimitiveValue<bool>() ?? false;
        }
    }

    private void LoadRanksConfig()
    {
        string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");
        string defaultContent = GetDefaultRanksContent();

        try
        {
            if (!File.Exists(ranksFilePath))
            {
                File.WriteAllText(ranksFilePath, defaultContent);
                Logger.LogInformation("Created default ranks configuration file at {Path}", ranksFilePath);
            }

            string fileContent = File.ReadAllText(ranksFilePath);
            string jsonContent = RemoveJsonComments(fileContent);

            Ranks = JsonConvert.DeserializeObject<List<Rank>>(jsonContent) ?? [];

            if (Ranks.Count == 0)
            {
                File.WriteAllText(ranksFilePath, defaultContent);
                jsonContent = RemoveJsonComments(defaultContent);
                Ranks = JsonConvert.DeserializeObject<List<Rank>>(jsonContent) ?? [];

                Logger.LogWarning("No ranks found, restored default ranks.");
            }

            // Assign IDs and process colors
            for (int i = 0; i < Ranks.Count; i++)
            {
                Ranks[i].Id = i + 1;
                Ranks[i].ChatColor = ChatColor.ReplaceColors(Ranks[i].ChatColor);
            }

            // Sort by points
            Ranks = [.. Ranks.OrderBy(r => r.Point)];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load ranks configuration");
            Ranks = GetMinimalDefaultRanks();
        }
    }

    private void RegisterCommands()
    {
        // Player commands
        Commands.Register(Config.Settings.RankCommands, "Show your rank information", OnRankCommand, usage: CommandUsage.CLIENT_ONLY);

        // Admin commands
        Commands.Register(["zgivepoint", "zgivepoints"], "Give points to a player", OnGivePoints,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <amount>", permission: "@k4-ranks/admin");

        Commands.Register(["ztakepoint", "ztakepoints"], "Take points from a player", OnTakePoints,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <amount>", permission: "@k4-ranks/admin");

        Commands.Register(["zsetpoint", "zsetpoints"], "Set points for a player", OnSetPoints,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <amount>", permission: "@k4-ranks/admin");

        Commands.Register(["zresetpoint", "zresetpoints"], "Reset a player's points", OnResetPoints,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>", permission: "@k4-ranks/admin");
    }

    private void RegisterPlaceholders()
    {
        Placeholders.RegisterPlayer("{rank_color}", GetRankColor);
        Placeholders.RegisterPlayer("{rank}", GetRankName);
        Placeholders.RegisterPlayer("{points}", GetPlayerPoints);
    }

    private void RegisterEvents()
    {
        // Map and round events
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);

        // Player connection events
        RegisterEventHandler<EventPlayerActivate>(OnPlayerActivate);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);

        // Game events for points
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundMvp>(OnRoundMvp);
        RegisterEventHandler<EventHostageRescued>(OnHostageRescued);
        RegisterEventHandler<EventBombDefused>(OnBombDefused);
        RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
        RegisterEventHandler<EventHostageKilled>(OnHostageKilled);
        RegisterEventHandler<EventHostageHurt>(OnHostageHurt);
        RegisterEventHandler<EventBombPickup>(OnBombPickup);
        RegisterEventHandler<EventBombDropped>(OnBombDropped);
        RegisterEventHandler<EventBombExploded>(OnBombExploded);
        RegisterEventHandler<EventHostageRescuedAll>(OnHostageRescuedAll);

        Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataLoad, args =>
        {
            // Accept unified events ("*") and our own plugin events
            if (args.OwnerPlugin != ModuleName && args.OwnerPlugin != "*")
                return HookResult.Continue;

            var player = Player.Find(args.SteamId);
            if (player != null)
            {
                var data = player.Storage<PlayerRankData>();
                UpdatePlayerRank(player, data.Points);
            }

            return HookResult.Continue;
        });

        // Scoreboard updates if allowed
        if (!CoreConfig.FollowCS2ServerGuidelines && Config.Settings.UseScoreboardRanks)
        {
            RegisterListener<Listeners.OnTick>(UpdateScoreboards);

            if (ConVar.Find("mp_halftime")?.GetPrimitiveValue<bool>() == false)
                Logger.LogWarning("Halftime is disabled, this may cause scoreboard rendering issues.");
        }
    }

    private void SetupTimers()
    {
        // Scoreboard update timer
        AddTimer(5.0f, () =>
        {
            UserMessage message = UserMessage.FromId(350);
            message.Recipients.AddAllPlayers();
            message.Send();
        }, TimerFlags.REPEAT);

        // Playtime points timer
        if (Config.Points.PlaytimeInterval > 0)
        {
            AddTimer(60.0f, () =>
            {
                if (Config.Settings.WarmupPoints && GameRules?.WarmupPeriod != false)
                    return;

                if ((DateTime.Now - _lastPlaytimeCheck).TotalMinutes >= Config.Points.PlaytimeInterval)
                {
                    foreach (var player in Player.ValidLoop())
                    {
                        ModifyPlayerPoints(player, Config.Points.PlaytimePoints, "k4.events.playtime");
                    }

                    _lastPlaytimeCheck = DateTime.Now;
                }
            }, TimerFlags.REPEAT);
        }
    }

    private void OnMapStart(string mapName)
    {
        _isGameEnd = false;
        AddTimer(1.0f, () =>
        {
            GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
            FFAMode = ConVar.Find("mp_teammates_are_enemies")?.GetPrimitiveValue<bool>() ?? false;
        });
    }

    public bool ShouldProcessEvent()
    {
        _shouldProcessEvents = Player.List.Count >= Config.Settings.MinPlayers && (Config.Settings.WarmupPoints || GameRules?.WarmupPeriod != true);
        return _shouldProcessEvents;
    }

    // Placeholder implementations
    private string GetRankColor(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer != null)
        {
            var data = customPlayer.Storage<PlayerRankData>();
            var (rank, _) = GetRanksByPoints(data.Points);
            return rank?.ChatColor ?? ChatColors.Default.ToString();
        }

        return ChatColors.Default.ToString();
    }

    private string GetRankName(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer != null)
        {
            var data = customPlayer.Storage<PlayerRankData>();
            var (rank, _) = GetRanksByPoints(data.Points);
            return Localizer.ForPlayer(player, rank?.Name ?? "k4.phrases.rank.none");
        }

        return Localizer.ForPlayer(player, "k4.phrases.rank.none");
    }

    private string GetPlayerPoints(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer != null)
        {
            var data = customPlayer.Storage<PlayerRankData>();
            return data.Points.ToString("N0");
        }

        return "0";
    }

    private static string RemoveJsonComments(string json)
    {
        // Remove both single-line and multi-line comments in one pass
        return Regex.Replace(json, @"(//.*$)|(/\*[\s\S]*?\*/)", "", RegexOptions.Multiline);
    }
}