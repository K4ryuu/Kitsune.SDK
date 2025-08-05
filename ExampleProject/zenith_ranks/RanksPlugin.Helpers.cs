namespace K4_Zenith_Ranks;

public partial class RanksPlugin
{
    private static string GetDefaultRanksContent()
    {
        return @"[
    {
        ""Name"": ""Silver I"",
        ""Image"": """", // Image URL for the rank. This can be used for web integrations such as GameCMS
        ""Point"": 0, // From this amount of experience, the player is Silver I, if its 0, this will be the default rank
        ""ChatColor"": ""grey"", // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
        ""HexColor"": ""#C0C0C0"" // Hexadecimal color code for the rank
    },
    {
        ""Name"": ""Silver II"",
        ""Image"": """",
        ""Point"": 1500,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver III"",
        ""Image"": """",
        ""Point"": 3000,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver IV"",
        ""Image"": """",
        ""Point"": 4500,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver Elite"",
        ""Image"": """",
        ""Point"": 6000,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Silver Elite Master"",
        ""Image"": """",
        ""Point"": 8000,
        ""ChatColor"": ""grey"",
        ""HexColor"": ""#C0C0C0""
    },
    {
        ""Name"": ""Gold Nova I"",
        ""Image"": """",
        ""Point"": 10000,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Gold Nova II"",
        ""Image"": """",
        ""Point"": 13000,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Gold Nova III"",
        ""Image"": """",
        ""Point"": 17000,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Gold Nova Master"",
        ""Image"": """",
        ""Point"": 22000,
        ""ChatColor"": ""gold"",
        ""HexColor"": ""#FFD700""
    },
    {
        ""Name"": ""Master Guardian I"",
        ""Image"": """",
        ""Point"": 28000,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Master Guardian II"",
        ""Image"": """",
        ""Point"": 35000,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Master Guardian Elite"",
        ""Image"": """",
        ""Point"": 43000,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Distinguished Master Guardian"",
        ""Image"": """",
        ""Point"": 52000,
        ""ChatColor"": ""green"",
        ""HexColor"": ""#00FF00""
    },
    {
        ""Name"": ""Legendary Eagle"",
        ""Image"": """",
        ""Point"": 62000,
        ""ChatColor"": ""blue"",
        ""HexColor"": ""#0000FF""
    },
    {
        ""Name"": ""Legendary Eagle Master"",
        ""Image"": """",
        ""Point"": 70000,
        ""ChatColor"": ""blue"",
        ""HexColor"": ""#0000FF""
    },
    {
        ""Name"": ""Supreme Master First Class"",
        ""Image"": """",
        ""Point"": 75000,
        ""ChatColor"": ""purple"",
        ""HexColor"": ""#800080""
    },
    {
        ""Name"": ""Global Elite"",
        ""Image"": """",
        ""Point"": 80000,
        ""ChatColor"": ""lightred"",
        ""HexColor"": ""#FF4040""
    }
]";
    }

    private static List<Rank> GetMinimalDefaultRanks()
    {
        return
        [
            new() {
                Id = 1,
                Name = "Unranked",
                Point = 0,
                ChatColor = "default",
                HexColor = "#FFFFFF"
            }
        ];
    }
}