using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Core.Attributes.Version;
using System.Collections.Concurrent;
using Kitsune.SDK.Utilities;

namespace K4_Zenith_ExtendedCommands;

[MinimumApiVersion(300)]
[MinimumSdkVersion(1)]
public sealed partial class ExtendedCommandsPlugin : SdkPlugin, ISdkConfig<ExtendedCommandsConfig>
{
    public override string ModuleName => "K4-Zenith | Extended Commands";
    public override string ModuleAuthor => "K4ryuu @ KitsuneLab";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleDescription => "Extended administrative commands for server management";

    public new ExtendedCommandsConfig Config => GetTypedConfig<ExtendedCommandsConfig>();

    private readonly ConcurrentDictionary<CCSPlayerController, DeathLocation> _deathLocations = new();
    private static readonly Random _random = new();

    protected override void SdkLoad(bool hotReload)
    {
        ChatProcessor.TryInject(this);

        RegisterCommands();
        RegisterEvents();
    }

    protected override void SdkUnload(bool hotReload)
    {
        _deathLocations.Clear();
    }

    private void RegisterEvents()
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (@event.Userid == null || !@event.Userid.IsValid)
            return HookResult.Continue;

        var player = @event.Userid;
        var pawn = player.PlayerPawn?.Value;
        if (pawn == null)
            return HookResult.Continue;

        _deathLocations[player] = new DeathLocation
        {
            Position = pawn.AbsOrigin ?? new Vector(0, 0, 0),
            Angle = pawn.AbsRotation ?? new QAngle(0, 0, 0),
            Time = DateTime.Now
        };

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid != null && @event.Userid.IsValid)
        {
            _deathLocations.TryRemove(@event.Userid, out _);
        }

        return HookResult.Continue;
    }
}