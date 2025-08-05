using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes;
using Kitsune.SDK.Core.Attributes.Config;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Core.Models.Config;
using Kitsune.SDK.Services.Config;
using MySqlConnector;

namespace Kitsune.Examples
{
	/// <summary>
	/// Example showing config handler functionality
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class ConfigExample : SdkPlugin, ISdkConfig<ExampleConfig>
	{
		public override string ModuleName => "Kitsune Config Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Example demonstrating config handler usage";

		// ISdkConfig<ExampleConfig> implementation
		public new ExampleConfig Config => GetTypedConfig<ExampleConfig>();

		protected override void SdkLoad(bool hotReload)
		{
			// Register test commands
			RegisterCommands();
		}

		private void RegisterCommands()
		{
			// View/set config value
			Commands.Register(
				"config",
				"View or set config values",
				HandleConfigCommand,
				argCount: 1,
				helpText: "config <key> [value]",
				permission: "@kitsune/admin"
			);

			// Test global config access
			Commands.Register(
				"globalconfig",
				"Test global config access",
				HandleGlobalConfigCommand,
				argCount: 1,
				helpText: "globalconfig <key>"
			);

			// Check if config exists
			Commands.Register(
				"hasconfig",
				"Check if a config exists",
				HandleHasConfigCommand,
				argCount: 1,
				helpText: "hasconfig <key>"
			);

			// Get database connection test
			Commands.Register(
				"testdb",
				"Test database connection",
				HandleTestDbCommand,
				permission: "@kitsune/admin"
			);
		}

		private void HandleConfigCommand(CCSPlayerController? controller, CommandInfo info)
		{
			string key = info.GetArg(1);
			string? value = info.ArgCount > 2 ? info.GetArg(2) : null;

			if (value == null)
			{
				// View config value - demonstrate accessing type-safe config properties
				try
				{
					switch (key.ToLower())
					{
						case "server_name":
							info.ReplyToCommand($" {ChatColors.Gold}Config: {key}");
							info.ReplyToCommand($" {ChatColors.Green}Value: {Config.ServerName}");
							break;
						case "max_players":
							info.ReplyToCommand($" {ChatColors.Gold}Config: {key}");
							info.ReplyToCommand($" {ChatColors.Green}Value: {Config.MaxPlayers}");
							break;
						case "round_time":
							info.ReplyToCommand($" {ChatColors.Gold}Config: {key}");
							info.ReplyToCommand($" {ChatColors.Green}Value: {Config.RoundTime}");
							break;
						case "warmup_time":
							info.ReplyToCommand($" {ChatColors.Gold}Config: {key}");
							info.ReplyToCommand($" {ChatColors.Green}Value: {Config.WarmupTime}");
							break;
						case "friendly_fire":
							info.ReplyToCommand($" {ChatColors.Gold}Config: {key}");
							info.ReplyToCommand($" {ChatColors.Green}Value: {Config.FriendlyFire}");
							break;
						case "gravity":
							info.ReplyToCommand($" {ChatColors.Gold}Config: {key}");
							info.ReplyToCommand($" {ChatColors.Green}Value: {Config.Gravity}");
							break;
						default:
							info.ReplyToCommand($" {ChatColors.Red}Config '{key}' not found");
							break;
					}
				}
				catch (Exception ex)
				{
					info.ReplyToCommand($" {ChatColors.Red}Error: {ex.Message}");
				}
			}
			else
			{
				// Setting config values - note that type-safe config properties are
				// automatically updated when the underlying config values change
				info.ReplyToCommand($" {ChatColors.Yellow}Config values are automatically updated when changed in the config files");
				info.ReplyToCommand($" {ChatColors.Yellow}Direct runtime modification is not recommended for type-safe configs");
			}
		}

		private void HandleGlobalConfigCommand(CCSPlayerController? controller, CommandInfo info)
		{
			string key = info.GetArg(1);

			// Demonstrate accessing global configs from this plugin
			if (key.Equals("global_prefix", StringComparison.OrdinalIgnoreCase))
			{
				info.ReplyToCommand($" {ChatColors.Gold}Global Prefix: {ChatColors.White}{Config.GlobalPrefix}");
			}
			else if (key.Equals("global_currency", StringComparison.OrdinalIgnoreCase))
			{
				info.ReplyToCommand($" {ChatColors.Gold}Global Currency: {ChatColors.White}{Config.GlobalCurrency}");
			}
			else
			{
				// Try to get a global config from another plugin
				var apiEndpoint = base.Config.GetGlobalValue<string>("ConfigExample", "api_endpoint");
				if (apiEndpoint != null)
				{
					info.ReplyToCommand($" {ChatColors.Gold}API Endpoint (from ConfigExample): {ChatColors.White}{apiEndpoint}");
				}
				else
				{
					info.ReplyToCommand($" {ChatColors.Red}Global config '{key}' not found");
				}
			}
		}

		private void HandleHasConfigCommand(CCSPlayerController? controller, CommandInfo info)
		{
			string key = info.GetArg(1);

			// With type-safe configs, we can check if a property exists
			var configType = typeof(ExampleConfig);
			var properties = configType.GetProperties();
			bool exists = properties.Any(p =>
			{
				var configAttr = p.GetCustomAttribute<ConfigAttribute>();
				return configAttr != null && configAttr.Name.Equals(key, StringComparison.OrdinalIgnoreCase);
			});

			info.ReplyToCommand($" {ChatColors.Gold}Config check for '{key}':");
			info.ReplyToCommand($" {ChatColors.Green}Exists in this plugin: {exists}");
		}

		private void HandleTestDbCommand(CCSPlayerController? controller, CommandInfo info)
		{
			try
			{
				// Create database connection using config - Use in async, but for the example we use sync
				using var connection = new MySqlConnection(base.Config.GetConnectionString());
				connection.Open();

				info.ReplyToCommand($" {ChatColors.Green}Database connection successful!");
				info.ReplyToCommand($" {ChatColors.Blue}Connected to: {connection.Database}");

				connection.Close();
			}
			catch (Exception ex)
			{
				info.ReplyToCommand($" {ChatColors.Red}Database connection failed: {ex.Message}");
			}
		}

		protected override void SdkUnload(bool hotReload)
		{
			// ! Resource cleanup is automatically handled by SdkPlugin
		}
	}

	/// <summary>
	/// Configuration class with type-safe properties
	/// </summary>
	[RequiresDatabase(Optional = true)]
	public class ExampleConfig
	{
		// Basic configs in default group
		[Config("server_name", "Name of the server")]
		public string ServerName { get; set; } = "My Awesome Server";

		[Config("max_players", "Maximum number of players")]
		[ConfigValidation(nameof(ValidateMaxPlayers))]
		public int MaxPlayers { get; set; } = 32;

		[Config("round_time", "Round time in seconds")]
		public int RoundTime { get; set; } = 180;

		[Config("warmup_time", "Warmup time in seconds")]
		public int WarmupTime { get; set; } = 30;

		[Config("friendly_fire", "Enable friendly fire")]
		public bool FriendlyFire { get; set; } = false;

		[Config("auto_balance", "Enable team auto-balance")]
		public bool AutoBalance { get; set; } = true;

		// Gameplay configs in separate group
		[Config("gravity", "Gravity multiplier", groupName: "gameplay")]
		public float Gravity { get; set; } = 1.0f;

		[Config("speed_multiplier", "Player speed multiplier", groupName: "gameplay")]
		public float SpeedMultiplier { get; set; } = 1.0f;

		[Config("jump_height", "Jump height multiplier", groupName: "gameplay")]
		public float JumpHeight { get; set; } = 1.0f;

		[Config("fall_damage", "Enable fall damage", groupName: "gameplay")]
		public bool FallDamage { get; set; } = true;

		[Config("health_regen", "Health regeneration per second", groupName: "gameplay")]
		public int HealthRegen { get; set; } = 0;

		[Config("armor_effectiveness", "Armor damage reduction percentage", groupName: "gameplay")]
		public float ArmorEffectiveness { get; set; } = 0.5f;

		// Map configs
		[Config("allowed_maps", "List of allowed maps", groupName: "maps")]
		public List<string> AllowedMaps { get; set; } = new List<string>
		{
			"de_dust2",
			"de_mirage",
			"de_inferno",
			"de_nuke",
			"de_overpass"
		};

		[Config("map_weights", "Map selection weights", groupName: "maps")]
		public Dictionary<string, int> MapWeights { get; set; } = new Dictionary<string, int>
		{
			["de_dust2"] = 30,
			["de_mirage"] = 25,
			["de_inferno"] = 20,
			["de_nuke"] = 15,
			["de_overpass"] = 10
		};

		// VIP system configs
		[Config("vip_enabled", "Enable VIP system", groupName: "vip")]
		public bool VipEnabled { get; set; } = true;

		[Config("vip_health_bonus", "Extra health for VIP players", groupName: "vip")]
		public int VipHealthBonus { get; set; } = 25;

		[Config("vip_armor_bonus", "Extra armor for VIP players", groupName: "vip")]
		public int VipArmorBonus { get; set; } = 50;

		[Config("vip_spawn_money", "Extra spawn money for VIP players", groupName: "vip")]
		public int VipSpawnMoney { get; set; } = 1000;

		[Config("vip_features", "List of VIP features", groupName: "vip")]
		public List<string> VipFeatures { get; set; } = new List<string>
		{
			"extra_health",
			"extra_armor",
			"spawn_money",
			"reserved_slot",
			"custom_tag"
		};

		// Protected configs
		[Config("api_endpoint", "API server endpoint", ConfigFlag.Global | ConfigFlag.Protected, groupName: "api")]
		public string ApiEndpoint { get; set; } = "https://api.example.com";

		[Config("api_timeout", "API timeout in seconds", ConfigFlag.Global | ConfigFlag.Protected, groupName: "api")]
		public int ApiTimeout { get; set; } = 30;

		[Config("admin_password", "Admin password", ConfigFlag.Protected, groupName: "security")]
		public string AdminPassword { get; set; } = "admin123";

		// Locked config
		[Config("plugin_version", "Plugin version", ConfigFlag.Global | ConfigFlag.Locked, groupName: "system")]
		public string PluginVersion { get; set; } = "1.0.0";

		// Global configs
		[Config("global_prefix", "Global chat prefix", ConfigFlag.Global, groupName: "global")]
		public string GlobalPrefix { get; set; } = "[Server]";

		[Config("global_currency", "Server currency name", ConfigFlag.Global, groupName: "global")]
		public string GlobalCurrency { get; set; } = "credits";

		[Config("global_website", "Server website URL", ConfigFlag.Global, groupName: "global")]
		public string GlobalWebsite { get; set; } = "https://example.com";

		[Config("global_discord", "Discord invite link", ConfigFlag.Global, groupName: "global")]
		public string GlobalDiscord { get; set; } = "https://discord.gg/example";

		// Validation method - must be private/public, returning bool and taking one parameter of the property type
		private bool ValidateMaxPlayers(int value)
		{
			// Max players must be between 1 and 64
			return value >= 1 && value <= 64;
		}
	}
}