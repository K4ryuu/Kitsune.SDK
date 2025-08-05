using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Config;

namespace K4_Zenith_ExtendedCommands;

public class ExtendedCommandsConfig
{
    [Config("ActivityDisplay", "Admin activity display mode (0=None, 1=Admin name only, 2=Admin name to admins/Console to players, 3=Admin name to all, 4=Anonymous to all, 5=Anonymous to players/Name to admins)")]
    public int ActivityDisplay { get; set; } = 3;
}

public struct DeathLocation
{
    public Vector Position { get; set; }
    public QAngle Angle { get; set; }
    public DateTime Time { get; set; }
}