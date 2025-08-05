namespace K4_Zenith_ExtendedCommands;

public enum WeaponType
{
    Knife,
    Pistol,
    Rifle,
    Grenade,
    C4,
    Other
}

public class WeaponInfo
{
    public required string ClassName { get; init; }
    public required string DisplayName { get; init; }
    public required WeaponType Type { get; init; }
    public List<string> Aliases { get; init; } = new();
}

public static class WeaponData
{
    public static readonly Dictionary<string, WeaponInfo> WeaponInfoMap = new()
    {
        // Knives
        ["knife"] = new() { ClassName = "weapon_knife", DisplayName = "Knife", Type = WeaponType.Knife, Aliases = ["knife_t", "knife_ct"] },
        ["taser"] = new() { ClassName = "weapon_taser", DisplayName = "Zeus x27", Type = WeaponType.Other, Aliases = ["zeus"] },

        // Pistols
        ["glock"] = new() { ClassName = "weapon_glock", DisplayName = "Glock-18", Type = WeaponType.Pistol, Aliases = ["glock18"] },
        ["usp_silencer"] = new() { ClassName = "weapon_usp_silencer", DisplayName = "USP-S", Type = WeaponType.Pistol, Aliases = ["usp", "usps"] },
        ["hkp2000"] = new() { ClassName = "weapon_hkp2000", DisplayName = "P2000", Type = WeaponType.Pistol, Aliases = ["p2000"] },
        ["elite"] = new() { ClassName = "weapon_elite", DisplayName = "Dual Berettas", Type = WeaponType.Pistol, Aliases = ["dualies", "dualberettas"] },
        ["p250"] = new() { ClassName = "weapon_p250", DisplayName = "P250", Type = WeaponType.Pistol },
        ["tec9"] = new() { ClassName = "weapon_tec9", DisplayName = "Tec-9", Type = WeaponType.Pistol },
        ["fiveseven"] = new() { ClassName = "weapon_fiveseven", DisplayName = "Five-SeveN", Type = WeaponType.Pistol, Aliases = ["57"] },
        ["cz75a"] = new() { ClassName = "weapon_cz75a", DisplayName = "CZ75-Auto", Type = WeaponType.Pistol, Aliases = ["cz", "cz75"] },
        ["deagle"] = new() { ClassName = "weapon_deagle", DisplayName = "Desert Eagle", Type = WeaponType.Pistol, Aliases = ["deserteagle"] },
        ["revolver"] = new() { ClassName = "weapon_revolver", DisplayName = "R8 Revolver", Type = WeaponType.Pistol, Aliases = ["r8"] },

        // SMGs
        ["mac10"] = new() { ClassName = "weapon_mac10", DisplayName = "MAC-10", Type = WeaponType.Rifle },
        ["mp9"] = new() { ClassName = "weapon_mp9", DisplayName = "MP9", Type = WeaponType.Rifle },
        ["mp7"] = new() { ClassName = "weapon_mp7", DisplayName = "MP7", Type = WeaponType.Rifle },
        ["mp5sd"] = new() { ClassName = "weapon_mp5sd", DisplayName = "MP5-SD", Type = WeaponType.Rifle, Aliases = ["mp5"] },
        ["ump45"] = new() { ClassName = "weapon_ump45", DisplayName = "UMP-45", Type = WeaponType.Rifle, Aliases = ["ump"] },
        ["p90"] = new() { ClassName = "weapon_p90", DisplayName = "P90", Type = WeaponType.Rifle },
        ["bizon"] = new() { ClassName = "weapon_bizon", DisplayName = "PP-Bizon", Type = WeaponType.Rifle, Aliases = ["ppbizon"] },

        // Rifles
        ["galil"] = new() { ClassName = "weapon_galilar", DisplayName = "Galil AR", Type = WeaponType.Rifle, Aliases = ["galilar"] },
        ["famas"] = new() { ClassName = "weapon_famas", DisplayName = "FAMAS", Type = WeaponType.Rifle },
        ["ak47"] = new() { ClassName = "weapon_ak47", DisplayName = "AK-47", Type = WeaponType.Rifle, Aliases = ["ak"] },
        ["m4a4"] = new() { ClassName = "weapon_m4a1", DisplayName = "M4A4", Type = WeaponType.Rifle, Aliases = ["m4a1"] },
        ["m4a1_silencer"] = new() { ClassName = "weapon_m4a1_silencer", DisplayName = "M4A1-S", Type = WeaponType.Rifle, Aliases = ["m4a1s"] },
        ["ssg08"] = new() { ClassName = "weapon_ssg08", DisplayName = "SSG 08", Type = WeaponType.Rifle, Aliases = ["scout"] },
        ["sg556"] = new() { ClassName = "weapon_sg556", DisplayName = "SG 553", Type = WeaponType.Rifle, Aliases = ["sg553", "krieg"] },
        ["aug"] = new() { ClassName = "weapon_aug", DisplayName = "AUG", Type = WeaponType.Rifle },
        ["awp"] = new() { ClassName = "weapon_awp", DisplayName = "AWP", Type = WeaponType.Rifle },
        ["g3sg1"] = new() { ClassName = "weapon_g3sg1", DisplayName = "G3SG1", Type = WeaponType.Rifle },
        ["scar20"] = new() { ClassName = "weapon_scar20", DisplayName = "SCAR-20", Type = WeaponType.Rifle, Aliases = ["scar"] },

        // Heavy
        ["nova"] = new() { ClassName = "weapon_nova", DisplayName = "Nova", Type = WeaponType.Rifle },
        ["xm1014"] = new() { ClassName = "weapon_xm1014", DisplayName = "XM1014", Type = WeaponType.Rifle, Aliases = ["xm"] },
        ["sawedoff"] = new() { ClassName = "weapon_sawedoff", DisplayName = "Sawed-Off", Type = WeaponType.Rifle },
        ["mag7"] = new() { ClassName = "weapon_mag7", DisplayName = "MAG-7", Type = WeaponType.Rifle },
        ["m249"] = new() { ClassName = "weapon_m249", DisplayName = "M249", Type = WeaponType.Rifle },
        ["negev"] = new() { ClassName = "weapon_negev", DisplayName = "Negev", Type = WeaponType.Rifle },

        // Grenades
        ["hegrenade"] = new() { ClassName = "weapon_hegrenade", DisplayName = "HE Grenade", Type = WeaponType.Grenade, Aliases = ["he", "hegren"] },
        ["flashbang"] = new() { ClassName = "weapon_flashbang", DisplayName = "Flashbang", Type = WeaponType.Grenade, Aliases = ["flash"] },
        ["smokegrenade"] = new() { ClassName = "weapon_smokegrenade", DisplayName = "Smoke Grenade", Type = WeaponType.Grenade, Aliases = ["smoke"] },
        ["molotov"] = new() { ClassName = "weapon_molotov", DisplayName = "Molotov", Type = WeaponType.Grenade },
        ["incgrenade"] = new() { ClassName = "weapon_incgrenade", DisplayName = "Incendiary Grenade", Type = WeaponType.Grenade, Aliases = ["inc"] },
        ["decoy"] = new() { ClassName = "weapon_decoy", DisplayName = "Decoy Grenade", Type = WeaponType.Grenade },

        // Other
        ["c4"] = new() { ClassName = "weapon_c4", DisplayName = "C4", Type = WeaponType.C4 },
        ["healthshot"] = new() { ClassName = "weapon_healthshot", DisplayName = "Healthshot", Type = WeaponType.Other },
    };

    static WeaponData()
    {
        // Build reverse lookup for aliases
        var aliasLookup = new Dictionary<string, string>();
        foreach (var (key, info) in WeaponInfoMap)
        {
            foreach (var alias in info.Aliases)
            {
                aliasLookup[alias] = key;
            }
        }

        // Add alias entries to main map
        foreach (var (alias, key) in aliasLookup)
        {
            if (WeaponInfoMap.TryGetValue(key, out var info))
            {
                WeaponInfoMap[alias] = info;
            }
        }
    }
}