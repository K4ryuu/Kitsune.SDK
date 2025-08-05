using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4_Zenith_ExtendedCommands;

public sealed partial class ExtendedCommandsPlugin
{
    private void RegisterCommands()
    {
        // Health & Armor Commands
        Commands.Register(["hp", "health"], "Sets player health to a given value", OnHealthCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <health>",
            permission: "@zenith-commands/health");

        Commands.Register(["armor"], "Sets player armor to a given value", OnArmorCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <armor>",
            permission: "@zenith-commands/armor");

        // Movement Commands
        Commands.Register(["freeze"], "Freezes a player", OnFreezeCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/freeze");

        Commands.Register(["unfreeze"], "Unfreezes a player", OnUnfreezeCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/unfreeze");

        Commands.Register(["speed"], "Sets player speed", OnSpeedCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <speed>",
            permission: "@zenith-commands/speed");

        Commands.Register(["noclip"], "Toggles noclip mode for a player", OnNoclipCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/noclip");

        // Life State Commands
        Commands.Register(["slay", "kill"], "Kills a player", OnSlayCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/slay");

        Commands.Register(["respawn"], "Respawns a player", OnRespawnCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/respawn");

        Commands.Register(["revive"], "Revives a player at their death location", OnReviveCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/revive");

        // Teleportation Commands
        Commands.Register(["tp", "teleport"], "Teleports a player to another player", OnTeleportCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <destination>",
            permission: "@zenith-commands/teleport");

        Commands.Register(["tppos"], "Teleports a player to coordinates", OnTeleportPosCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 4, helpText: "<target> <x> <y> <z>",
            permission: "@zenith-commands/teleport");

        Commands.Register(["bury"], "Buries a player", OnBuryCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/bury");

        Commands.Register(["unbury"], "Unburies a player", OnUnburyCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/unbury");

        // Team Commands
        Commands.Register(["team"], "Moves a player to a team", OnTeamCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <team>",
            permission: "@zenith-commands/team");

        Commands.Register(["swap"], "Swaps a player's team", OnSwapCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/swap");

        // Visual Effects
        Commands.Register(["blind"], "Blinds a player", OnBlindCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target> [duration]",
            permission: "@zenith-commands/blind");

        Commands.Register(["unblind"], "Unblinds a player", OnUnblindCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/unblind");

        Commands.Register(["slap"], "Slaps a player", OnSlapCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target> [damage]",
            permission: "@zenith-commands/slap");

        // Weapon Commands
        Commands.Register(["give"], "Gives a weapon to a player", OnGiveCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <weapon>",
            permission: "@zenith-commands/give");

        Commands.Register(["strip"], "Strips all weapons from a player", OnStripCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/strip");

        // Visibility Commands
        Commands.Register(["god", "godmode"], "Toggles god mode for a player", OnGodCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/god");

        Commands.Register(["stealth", "hide"], "Toggles stealth mode for a player", OnStealthCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<target>",
            permission: "@zenith-commands/stealth");

        // Player Properties
        Commands.Register(["rename"], "Renames a player", OnRenameCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<target> <name>",
            permission: "@zenith-commands/rename");

        // Server Commands
        Commands.Register(["rcon"], "Executes a server command", OnRconCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 1, helpText: "<command>",
            permission: "@zenith-commands/rcon");

        Commands.Register(["cvar"], "Sets a convar value", OnCvarCommand,
            usage: CommandUsage.CLIENT_AND_SERVER, argCount: 2, helpText: "<cvar> <value>",
            permission: "@zenith-commands/cvar");

        // Anti-Cheat Commands
        Commands.Register(["ghosting", "checkip"], "Lists players sharing IP addresses", OnGhostingCommand,
            usage: CommandUsage.CLIENT_AND_SERVER,
            permission: "@zenith-commands/ghosting");
    }

    private void OnHealthCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        if (!int.TryParse(command.GetArg(2), out int health) || health < 0)
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_health")}");
            return;
        }

        ProcessTargetAction(player, targetResult, "commands.health", health.ToString(), (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.Health = health;
            Utilities.SetStateChanged(target.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
        }, requireAlive: true);
    }

    private void OnArmorCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        if (!int.TryParse(command.GetArg(2), out int armor) || armor < 0)
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_armor")}");
            return;
        }

        ProcessTargetAction(player, targetResult, "commands.armor", armor.ToString(), (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.ArmorValue = armor;
            Utilities.SetStateChanged(target.PlayerPawn.Value, "CCSPlayerPawn", "m_ArmorValue");
        }, requireAlive: true);
    }

    private void OnFreezeCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.freeze", null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;
            Schema.SetSchemaValue(target.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 0);
            Utilities.SetStateChanged(target.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
        }, requireAlive: true);
    }

    private void OnUnfreezeCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.unfreeze", null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
            Schema.SetSchemaValue(target.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2);
            Utilities.SetStateChanged(target.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
        }, requireAlive: true);
    }

    private void OnSpeedCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        if (!float.TryParse(command.GetArg(2), out float speed) || speed < 0)
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_speed")}");
            return;
        }

        ProcessTargetAction(player, targetResult, "commands.speed", speed.ToString("F1"), (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.VelocityModifier = speed;
        }, requireAlive: true);
    }

    private void OnNoclipCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.noclip", null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;

            var currentMoveType = target.PlayerPawn.Value.MoveType;
            if (currentMoveType == MoveType_t.MOVETYPE_NOCLIP)
            {
                target.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
                Schema.SetSchemaValue(target.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2);
            }
            else
            {
                target.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NOCLIP;
                Schema.SetSchemaValue(target.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 8);
            }
            Utilities.SetStateChanged(target.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
        }, requireAlive: true);
    }

    private void OnSlayCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.slay", null, (target) =>
        {
            target.CommitSuicide(false, true);
        }, requireAlive: true);
    }

    private void OnRespawnCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.respawn", null, (target) =>
        {
            target.Respawn();
        }, requireDead: true);
    }

    private void OnReviveCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.revive", null, (target) =>
        {
            if (_deathLocations.TryGetValue(target, out var deathLocation))
            {
                target.Respawn();
                Server.NextFrame(() =>
                {
                    if (target.PlayerPawn?.Value != null)
                    {
                        target.PlayerPawn.Value.Teleport(deathLocation.Position, deathLocation.Angle, new Vector(0, 0, 0));
                    }
                });
            }
            else
            {
                target.Respawn();
            }
        }, requireDead: true);
    }

    private void OnTeleportCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        var destPattern = command.GetArg(2);
        var destResult = new Target(destPattern).GetTarget(player);

        if (destResult == null || destResult.Players.Count != 1)
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_destination")}");
            return;
        }

        var destination = destResult.Players.First();
        if (!destination.IsValid || !destination.PawnIsAlive || destination.PlayerPawn?.Value == null)
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.destination_not_alive")}");
            return;
        }

        var destPos = destination.PlayerPawn.Value.AbsOrigin;
        var destAngle = destination.PlayerPawn.Value.AbsRotation;

        ProcessTargetAction(player, targetResult, "commands.teleport", destination.PlayerName, (target) =>
        {
            if (target.PlayerPawn?.Value == null || destPos == null || destAngle == null) return;
            target.PlayerPawn.Value.Teleport(destPos, destAngle, new Vector(0, 0, 0));
        }, requireAlive: true);
    }

    private void OnTeleportPosCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null)
            return;

        if (!float.TryParse(command.GetArg(2), out float x) || !float.TryParse(command.GetArg(3), out float y) || !float.TryParse(command.GetArg(4), out float z))
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_coordinates")}");
            return;
        }

        var position = new Vector(x, y, z);
        ProcessTargetAction(player, targetResult, "commands.teleportpos", $"{x:F1}, {y:F1}, {z:F1}", (target) =>
        {
            if (target.PlayerPawn?.Value == null)
                return;

            target.PlayerPawn.Value.Teleport(position, target.PlayerPawn.Value.AbsRotation, new Vector(0, 0, 0));
        }, requireAlive: true);
    }

    private void OnBuryCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.bury", null, (target) =>
        {
            if (target.PlayerPawn?.Value?.AbsOrigin == null)
                return;

            var currentPos = target.PlayerPawn.Value.AbsOrigin;
            var buriedPos = new Vector(currentPos.X, currentPos.Y, currentPos.Z - 50);
            target.PlayerPawn.Value.Teleport(buriedPos, target.PlayerPawn.Value.AbsRotation, new Vector(0, 0, 0));
        }, requireAlive: true);
    }

    private void OnUnburyCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.unbury", null, (target) =>
        {
            if (target.PlayerPawn?.Value?.AbsOrigin == null) return;
            var currentPos = target.PlayerPawn.Value.AbsOrigin;
            var unburiedPos = new Vector(currentPos.X, currentPos.Y, currentPos.Z + 50);
            target.PlayerPawn.Value.Teleport(unburiedPos, target.PlayerPawn.Value.AbsRotation, new Vector(0, 0, 0));
        }, requireAlive: true);
    }

    private void OnTeamCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        var teamArg = command.GetArg(2).ToLower();
        CsTeam team = teamArg switch
        {
            "t" or "2" or "terrorist" => CsTeam.Terrorist,
            "ct" or "3" or "counterterrorist" => CsTeam.CounterTerrorist,
            "spec" or "1" or "spectator" => CsTeam.Spectator,
            _ => CsTeam.None
        };

        if (team == CsTeam.None)
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_team")}");
            return;
        }

        ProcessTargetAction(player, targetResult, "commands.team", team.ToString(), (target) =>
        {
            if (team == CsTeam.Spectator)
                target.ChangeTeam(team);
            else
                target.SwitchTeam(team);
        });
    }

    private void OnSwapCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.swap", null, (target) =>
        {
            var newTeam = target.Team switch
            {
                CsTeam.Terrorist => CsTeam.CounterTerrorist,
                CsTeam.CounterTerrorist => CsTeam.Terrorist,
                _ => target.Team
            };

            if (newTeam != target.Team)
            {
                target.SwitchTeam(newTeam);
            }
        });
    }

    private void OnBlindCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        float duration = 5.0f;
        if (command.ArgCount >= 3 && !float.TryParse(command.GetArg(2), out duration))
        {
            duration = 5.0f;
        }

        ProcessTargetAction(player, targetResult, "commands.blind", duration.ToString("F1"), (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.BlindUntilTime = Server.CurrentTime + duration;
        }, requireAlive: true);
    }

    private void OnUnblindCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.unblind", null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;
            target.PlayerPawn.Value.BlindUntilTime = 0;
        }, requireAlive: true);
    }

    private void OnSlapCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        int damage = 0;
        if (command.ArgCount >= 3 && !int.TryParse(command.GetArg(2), out damage))
        {
            damage = 0;
        }

        ProcessTargetAction(player, targetResult, "commands.slap", damage > 0 ? damage.ToString() : null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;

            // Apply damage if specified
            if (damage > 0)
            {
                target.PlayerPawn.Value.Health -= damage;
                if (target.PlayerPawn.Value.Health <= 0)
                {
                    target.CommitSuicide(false, true);
                    return;
                }
                Utilities.SetStateChanged(target.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
            }

            // Apply random velocity
            var velocity = new Vector(
                _random.Next(50, 230) * (_random.Next(2) == 0 ? 1 : -1),
                _random.Next(50, 230) * (_random.Next(2) == 0 ? 1 : -1),
                300
            );

            target.PlayerPawn.Value.AbsVelocity.X = velocity.X;
            target.PlayerPawn.Value.AbsVelocity.Y = velocity.Y;
            target.PlayerPawn.Value.AbsVelocity.Z = velocity.Z;
        }, requireAlive: true);
    }

    private void OnGiveCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        var weaponName = command.GetArg(2).ToLower();

        ProcessTargetAction(player, targetResult, "commands.give", weaponName, (target) =>
        {
            GiveWeapon(target, weaponName);
        }, requireAlive: true);
    }

    private void OnStripCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.strip", null, (target) =>
        {
            target.RemoveWeapons();
        }, requireAlive: true);
    }

    private void OnGodCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.god", null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;

            bool currentGodMode = target.PlayerPawn.Value.TakesDamage;
            target.PlayerPawn.Value.TakesDamage = !currentGodMode;
        }, requireAlive: true);
    }

    private void OnStealthCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        ProcessTargetAction(player, targetResult, "commands.stealth", null, (target) =>
        {
            if (target.PlayerPawn?.Value == null) return;

            // Toggle render mode for stealth
            var renderMode = target.PlayerPawn.Value.RenderMode;
            if (renderMode == RenderMode_t.kRenderTransAlpha)
            {
                target.PlayerPawn.Value.RenderMode = RenderMode_t.kRenderNormal;
                target.PrintToChat(Localizer["commands.stealth_disabled"]);
            }
            else
            {
                target.PlayerPawn.Value.RenderMode = RenderMode_t.kRenderTransAlpha;
                Schema.SetSchemaValue(target.PlayerPawn.Value.Handle, "CBaseModelEntity", "m_clrRender", System.Drawing.Color.FromArgb(0, 255, 255, 255));
                target.PrintToChat(Localizer["commands.stealth_enabled"]);
            }
        }, requireAlive: true);
    }

    private void OnRenameCommand(CCSPlayerController? player, CommandInfo command)
    {
        var targetResult = GetTargetResult(player, command, 0, true);
        if (targetResult == null) return;

        var newName = command.GetArg(2);
        if (string.IsNullOrWhiteSpace(newName))
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_name")}");
            return;
        }

        ProcessTargetAction(player, targetResult, "commands.rename", newName, (target) =>
        {
            target.PlayerName = newName;
            Utilities.SetStateChanged(target, "CBasePlayerController", "m_iszPlayerName");
        });
    }

    private void OnRconCommand(CCSPlayerController? player, CommandInfo command)
    {
        var commandStr = command.GetCommandString;
        var rconCommand = commandStr.Substring(commandStr.IndexOf(' ') + 1);

        Server.ExecuteCommand(rconCommand);
        command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.rcon_executed", rconCommand)}");
    }

    private void OnCvarCommand(CCSPlayerController? player, CommandInfo command)
    {
        var cvarName = command.GetArg(1);
        var cvarValue = command.GetArg(2);

        SetConvarValue(cvarName, cvarValue);
        command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.cvar_set", cvarName, cvarValue)}");
    }

    private void OnGhostingCommand(CCSPlayerController? player, CommandInfo command)
    {
        var playersByIp = new Dictionary<string, List<CCSPlayerController>>();

        foreach (var target in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV))
        {
            var ip = target.IpAddress?.Split(':')[0];
            if (string.IsNullOrEmpty(ip))
                continue;

            if (!playersByIp.ContainsKey(ip))
                playersByIp[ip] = new();

            playersByIp[ip].Add(target);
        }

        command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.ghosting_header")}");

        foreach (var (ip, players) in playersByIp.Where(kvp => kvp.Value.Count > 1))
        {
            var playerNames = string.Join(", ", players.Select(p => p.PlayerName));
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.ghosting_entry", ip, playerNames)}");
        }

        if (!playersByIp.Any(kvp => kvp.Value.Count > 1))
        {
            command.ReplyToCommand($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.ghosting_none")}");
        }
    }
}