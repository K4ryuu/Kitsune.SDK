using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Extensions.Player;

using Player = Kitsune.SDK.Core.Base.Player;

namespace K4_Zenith_TimeStats;

public sealed partial class TimeStatsPlugin
{
    private void RegisterEvents()
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
        RegisterEventHandler<EventPlayerActivate>(OnPlayerActivate);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataLoad, OnPlayerDataLoad);
        Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataSave, OnPlayerDataSave, HookMode.Pre);
    }

    private HookResult OnPlayerActivate(EventPlayerActivate @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        Player.GetOrCreate<Player>(player);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDataLoad(PlayerDataEventArgs args)
    {
        var k4Player = Player.Find(args.SteamId);
        if (k4Player == null)
            return HookResult.Continue;

        var timeData = k4Player.Storage<PlayerTimeData>();
        timeData.LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();

        // Initialize team and alive status from current player state
        var controller = k4Player.Controller;
        timeData.CurrentTeam = controller.Team;
        timeData.IsAlive = controller.PlayerPawn?.Value?.Health > 0;

        _playerTimes[controller] = timeData;
        return HookResult.Continue;
    }

    private HookResult OnPlayerDataSave(PlayerDataEventArgs args)
    {
        var k4Player = Player.Find(args.SteamId);
        if (k4Player == null)
            return HookResult.Continue;

        // Update playtime before save
        if (_playerTimes.TryGetValue(k4Player.Controller, out var timeData))
        {
            UpdatePlaytime(timeData);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid == null || !@event.Userid.IsValid)
            return HookResult.Continue;

        if (_playerTimes.TryGetValue(@event.Userid, out var timeData))
        {
            UpdatePlaytime(timeData);
            timeData.IsAlive = true;
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (@event.Userid == null || !@event.Userid.IsValid)
            return HookResult.Continue;

        if (_playerTimes.TryGetValue(@event.Userid, out var timeData))
        {
            UpdatePlaytime(timeData);
            timeData.IsAlive = false;
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (@event.Userid == null || !@event.Userid.IsValid)
            return HookResult.Continue;

        if (_playerTimes.TryGetValue(@event.Userid, out var timeData))
        {
            UpdatePlaytime(timeData);
            timeData.CurrentTeam = (CsTeam)@event.Team;
            timeData.IsAlive = @event.Team != (int)CsTeam.Spectator;
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid == null || !@event.Userid.IsValid)
            return HookResult.Continue;

        // Clean up the player from tracking dictionary
        _playerTimes.Remove(@event.Userid);

        Player.Find(@event.Userid)?.Dispose();
        return HookResult.Continue;
    }
}