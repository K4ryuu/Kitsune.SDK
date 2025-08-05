using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Cvars;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Extensions.Player;

namespace K4Zenith.Stats;

[MinimumApiVersion(300)]
[MinimumSdkVersion(1)]
public sealed partial class StatsPlugin : SdkPlugin, ISdkConfig<StatsConfig>, ISdkStorage<PlayerStatsData>
{
    public override string ModuleName => "K4-Zenith | Stats";
    public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleDescription => "Advanced statistics tracking system";

    // Type-safe config access through ISdkConfig<StatsConfig>
    public new StatsConfig Config => GetTypedConfig<StatsConfig>();

    // State
    public CCSGameRules? GameRules { get; private set; }
    private bool _firstBlood = false;
    public bool FFAMode = false;

    protected override void SdkLoad(bool hotReload)
    {
        // Initialize menu system
        InitializeMenu();

        // Register all components
        RegisterCommands();
        RegisterPlaceholders();
        RegisterEvents();

        // Handle hot reload
        if (hotReload)
        {
            FFAMode = ConVar.Find("mp_teammates_are_enemies")?.GetPrimitiveValue<bool>() ?? false;
        }
    }

    private void RegisterPlaceholders()
    {
        Placeholders.RegisterPlayer("{kda}", CalculateKDA);
        Placeholders.RegisterPlayer("{kpr}", CalculateKPR);
        Placeholders.RegisterPlayer("{accuracy}", CalculateAccuracy);
        Placeholders.RegisterPlayer("{kd}", CalculateKD);
    }

    private void RegisterEvents()
    {
        RegisterEventHandler<EventPlayerActivate>(OnPlayerActivate);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        // Map and round events
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);

        // Player events
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
        RegisterEventHandler<EventHostageRescued>(OnHostageRescued);
        RegisterEventHandler<EventHostageKilled>(OnHostageKilled);
        RegisterEventHandler<EventBombDefused>(OnBombDefused);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterEventHandler<EventRoundMvp>(OnRoundMvp);
        RegisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch, HookMode.Pre);
    }

    private void OnMapStart(string mapName)
    {
        AddTimer(1.0f, () =>
        {
            FFAMode = ConVar.Find("mp_teammates_are_enemies")?.GetPrimitiveValue<bool>() ?? false;
        });
    }

    public bool IsStatsAllowed()
    {
        GameRules ??= Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;
        return GameRules != null && (!GameRules.WarmupPeriod || Config.WarmupStats) && (Config.MinPlayers <= Player.List.Count);
    }

    public bool IsFirstBlood()
    {
        if (!_firstBlood)
        {
            _firstBlood = true;
            return true;
        }

        return false;
    }

    // Calculator methods for placeholders
    private string CalculateKD(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer == null) return "N/A";

        var stats = customPlayer.Storage<PlayerStatsData>();
        int kills = stats.Kills;
        int deaths = stats.Deaths;
        double kd = deaths == 0 ? kills : (double)kills / deaths;
        return kd.ToString("F2");
    }

    private string CalculateKDA(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer == null) return "N/A";

        var stats = customPlayer.Storage<PlayerStatsData>();
        int deaths = stats.Deaths;
        double kda = (stats.Kills + stats.Assists) / (double)(deaths == 0 ? 1 : deaths);
        return kda.ToString("F2");
    }

    private string CalculateKPR(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer == null) return "N/A";

        var stats = customPlayer.Storage<PlayerStatsData>();
        int kills = stats.Kills;
        int rounds = stats.RoundsOverall;
        double kpr = rounds == 0 ? kills : (double)kills / rounds;
        return kpr.ToString("F2");
    }

    private string CalculateAccuracy(CCSPlayerController player)
    {
        var customPlayer = Player.Find(player);
        if (customPlayer == null) return "N/A";

        var stats = customPlayer.Storage<PlayerStatsData>();
        int shoots = stats.Shoots;
        double accuracy = (shoots == 0) ? 0 : Math.Min((double)stats.HitsGiven / shoots * 100, 100);
        return accuracy.ToString("F2") + "%";
    }
}