using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4_Zenith_ExtendedCommands;

public sealed partial class ExtendedCommandsPlugin
{
    private TargetResult? GetTargetResult(CCSPlayerController? player, CommandInfo command, int argIndex = 0, bool allowMultiple = false)
    {
        var targetPattern = command.GetArg(argIndex + 1);
        var targetResult = new Target(targetPattern).GetTarget(player);

        if (targetResult == null)
        {
            command.ReplyToCommand(Localizer["commands.target_not_found", targetPattern]);
            return null;
        }

        if (!allowMultiple && targetResult.Players.Count > 1)
        {
            command.ReplyToCommand(Localizer["commands.multiple_targets"]);
            return null;
        }

        return targetResult;
    }

    private void ProcessTargetAction(CCSPlayerController? admin, TargetResult targetResult, string activityKey,
        string? activityValue, Action<CCSPlayerController> action, bool requireAlive = false, bool requireDead = false)
    {
        var validTargets = new List<CCSPlayerController>();

        foreach (var target in targetResult.Players)
        {
            if (!target.IsValid || target.IsBot || target.IsHLTV)
                continue;

            if (admin != null && !CanTargetPlayer(admin, target))
            {
                admin.PrintToChat($" {Localizer.ForPlayer(admin, "k4.general.prefix")} {Localizer.ForPlayer(admin, "commands.cannot_target", target.PlayerName)}");
                continue;
            }

            if (requireAlive && (!target.PawnIsAlive || target.PlayerPawn?.Value == null))
            {
                continue;
            }

            if (requireDead && target.PawnIsAlive)
            {
                continue;
            }

            validTargets.Add(target);
            action(target);
        }

        if (validTargets.Count > 0)
        {
            ShowActivityToPlayers(admin, activityKey, targetResult.Players.Count == 1 ? validTargets[0].PlayerName : "multiple targets", activityValue);
        }
    }

    private static bool CanTargetPlayer(CCSPlayerController admin, CCSPlayerController target)
    {
        if (admin == target)
            return true;

        var adminImmunity = AdminManager.GetPlayerImmunity(admin);
        var targetImmunity = AdminManager.GetPlayerImmunity(target);

        return adminImmunity >= targetImmunity;
    }

    private void ShowActivityToPlayers(CCSPlayerController? admin, string activityKey, string targetName, string? value = null)
    {
        if (Config.ActivityDisplay == 0)
            return;

        string adminName = admin?.PlayerName ?? "Console";
        bool isAdminValid = admin != null && admin.IsValid;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot || player.IsHLTV)
                continue;

            bool isPlayerAdmin = AdminManager.PlayerHasPermissions(player, "@css/admin");
            string displayName = adminName;

            switch (Config.ActivityDisplay)
            {
                case 1: // Admin name only to executing admin
                    if (player != admin)
                        continue;
                    break;

                case 2: // Admin name to admins, Console to players
                    if (!isPlayerAdmin && isAdminValid)
                        displayName = "Console";
                    break;

                case 3: // Admin name to all
                    // Use actual admin name
                    break;

                case 4: // Anonymous to all
                    displayName = "Admin";
                    break;

                case 5: // Anonymous to players, name to admins
                    if (!isPlayerAdmin)
                        displayName = "Admin";
                    break;

                default:
                    continue;
            }

            string message = value != null
                ? Localizer.ForPlayer(player, activityKey, displayName, targetName, value)
                : Localizer.ForPlayer(player, activityKey, displayName, targetName);

            player.PrintToChat($" {Localizer.ForPlayer(player, "k4.general.prefix")} {message}");
        }
    }

    private static void SetConvarValue(string convarName, string value)
    {
        var convar = ConVar.Find(convarName);
        if (convar == null)
            return;

        var flags = convar.Flags;
        convar.Flags &= ~ConVarFlags.FCVAR_CHEAT;

        Server.ExecuteCommand($"{convarName} {value}");

        convar.Flags = flags;
    }

    private static void RemoveWeapon(CCSPlayerController player, gear_slot_t? slot = null, string? className = null)
    {
        if (player.PlayerPawn.Value?.WeaponServices is null)
            return;

        List<CHandle<CBasePlayerWeapon>> weaponList = [.. player.PlayerPawn.Value.WeaponServices.MyWeapons];
        foreach (CHandle<CBasePlayerWeapon> weapon in weaponList)
        {
            if (weapon.IsValid && weapon.Value != null)
            {
                CCSWeaponBase ccsWeaponBase = weapon.Value.As<CCSWeaponBase>();
                if (ccsWeaponBase?.IsValid == true)
                {
                    CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;

                    if (weaponData == null || (slot != null && weaponData.GearSlot != slot) || (className != null && !ccsWeaponBase.DesignerName.Contains(className)))
                        continue;

                    player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Raw = weapon.Raw;
                    player.DropActiveWeapon();

                    Server.NextFrame(() =>
                    {
                        if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
                        {
                            ccsWeaponBase.AcceptInput("Kill");
                        }
                    });
                }
            }
        }
    }

    private static List<CBasePlayerWeapon> GetPlayerWeapons(CCSPlayerController player)
    {
        var weapons = new List<CBasePlayerWeapon>();

        if (player.PlayerPawn?.Value?.WeaponServices?.MyWeapons == null)
            return weapons;

        foreach (var weapon in player.PlayerPawn.Value.WeaponServices.MyWeapons)
        {
            if (weapon.IsValid && weapon.Value != null && weapon.Value.IsValid)
                weapons.Add(weapon.Value);
        }

        return weapons;
    }

    private void GiveWeapon(CCSPlayerController player, string weaponName)
    {
        if (!WeaponData.WeaponInfoMap.TryGetValue(weaponName.ToLower(), out var weaponInfo))
        {
            player.PrintToChat($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.invalid_weapon", weaponName)}");
            return;
        }

        // Check grenade limits
        if (weaponInfo.Type == WeaponType.Grenade)
        {
            var grenadeCount = GetPlayerWeapons(player).Count(w => w.DesignerName == weaponInfo.ClassName);
            var maxGrenades = GetMaxGrenades(weaponInfo.ClassName);
            var totalGrenades = ConVar.Find("ammo_grenade_limit_total")?.GetPrimitiveValue<int>() ?? 4;

            if (grenadeCount >= totalGrenades)
            {
                player.PrintToChat($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.grenade_limit", weaponInfo.DisplayName, totalGrenades)}");
                return;
            }

            if (grenadeCount >= maxGrenades)
            {
                player.PrintToChat($" {Localizer.ForPlayer(player, "k4.general.prefix")} {Localizer.ForPlayer(player, "commands.grenade_limit", weaponInfo.DisplayName, maxGrenades)}");
                return;
            }
        }

        // Strip existing weapon in slot if configured
        if (weaponInfo.Type == WeaponType.Rifle || weaponInfo.Type == WeaponType.Pistol)
        {
            var slot = weaponInfo.Type == WeaponType.Rifle ? gear_slot_t.GEAR_SLOT_RIFLE : gear_slot_t.GEAR_SLOT_PISTOL;
            RemoveWeapon(player, slot);
        }

        player.GiveNamedItem(weaponInfo.ClassName);
    }

    private static int GetMaxGrenades(string grenadeClass)
    {
        return grenadeClass switch
        {
            "weapon_flashbang" => ConVar.Find("ammo_grenade_limit_flashbang")?.GetPrimitiveValue<int>() ?? 2,
            "weapon_hegrenade" => ConVar.Find("ammo_grenade_limit_default")?.GetPrimitiveValue<int>() ?? 1,
            "weapon_smokegrenade" => ConVar.Find("ammo_grenade_limit_default")?.GetPrimitiveValue<int>() ?? 1,
            "weapon_molotov" => ConVar.Find("ammo_grenade_limit_default")?.GetPrimitiveValue<int>() ?? 1,
            "weapon_incgrenade" => ConVar.Find("ammo_grenade_limit_default")?.GetPrimitiveValue<int>() ?? 1,
            "weapon_decoy" => ConVar.Find("ammo_grenade_limit_default")?.GetPrimitiveValue<int>() ?? 1,
            "weapon_healthshot" => ConVar.Find("ammo_item_limit_healthshot")?.GetPrimitiveValue<int>() ?? 1,
            _ => 1
        };
    }
}