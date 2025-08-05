using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Core.Models.Events.Enums;
using Microsoft.Extensions.Logging;

namespace ExampleProject
{
	/// <summary>
	/// Example plugin showing how to use the SDK event system.
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class EventExample : SdkPlugin
	{
		public override string ModuleName => "Event Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Demonstrates the usage of the SDK event system";

		// Custom event names
		private const string CUSTOM_EVENT_PLAYER_ACTION = "player_action";
		private const string CUSTOM_EVENT_ROUND_END = "round_end";

		// ! We only use this to demonstrate the unsubscribe manual method, but this not required as the SdkPlugin will handle this automatically
		// ! Also you don't need to use a Guid, you can just register and you done. All handled automatically
		private Guid? _playerActionSubscriptionId;

		protected override void SdkLoad(bool hotReload)
		{
			// Player data load events
			Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataLoad, args =>
			{
				Logger.LogInformation($"Player data loading for SteamID: {args.SteamId}");

				// Only trigger this when our own plugin is the event owner or unified event, so hotreload another one wont affect us
				if (args.OwnerPlugin != ModuleName && args.OwnerPlugin != "*")
					return HookResult.Continue;

				// Example checking if an event should be blocked
				if (args.SteamId == 76561198043562034)
				{
					Logger.LogWarning($"Blocking data load for SteamID: {args.SteamId}");
					return HookResult.Handled;
				}

				return HookResult.Continue;
			}, HookMode.Pre);

			Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataLoad, args =>
			{
				if (args.OwnerPlugin != ModuleName && args.OwnerPlugin != "*")
					return HookResult.Continue;

				Logger.LogInformation($"Player data loaded for SteamID: {args.SteamId}");
				Logger.LogInformation($"Owner: {args.OwnerPlugin}");
				return HookResult.Continue;
			}, HookMode.Post);

			// Player data save events
			Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataSave, OnPlayerDataSaving, HookMode.Pre);
			Events.Subscribe<PlayerDataEventArgs>(EventType.PlayerDataSave, OnPlayerDataSaved, HookMode.Post);

			// Module events
			Events.Subscribe<ModuleLoadEventArgs>(EventType.ModuleLoad, OnModuleLoading, HookMode.Pre);
			Events.Subscribe<ModuleLoadEventArgs>(EventType.ModuleLoad, OnModuleLoaded, HookMode.Post);

			// Module unload events
			Events.Subscribe<ModuleUnloadEventArgs>(EventType.ModuleUnload, args =>
			{
				Logger.LogInformation($"Module unloading: {args.ModuleName}");
				Logger.LogInformation($"Reason: {args.Reason}");

				// Example: Detect dangerous unload conditions
				if (args.Reason.Contains("crash"))
				{
					Logger.LogCritical($"Emergency handling for module {args.ModuleName} due to crash");
					// HookResult.Changed indicates we've modified something but want to proceed
					return HookResult.Changed;
				}
				else if (args.ModuleName.Contains("CoreSystem") && !args.Reason.Contains("shutdown"))
				{
					Logger.LogWarning($"Attempt to unload critical module: {args.ModuleName}");
					// Try to stop the unload for critical modules but this won't work for server shutdown
					return HookResult.Stop;
				}

				return HookResult.Continue;
			}, HookMode.Pre);

			Events.Subscribe<ModuleUnloadEventArgs>(EventType.ModuleUnload, args =>
			{
				Logger.LogInformation($"Module unloaded: {args.ModuleName}");
				return HookResult.Continue;
			}, HookMode.Post);

			// Config events
			Events.Subscribe<ConfigLoadEventArgs>(EventType.ConfigLoad, args =>
			{
				Logger.LogInformation($"Config loading for module: {args.ModuleName}");
				Logger.LogInformation($"Config path: {args.ConfigPath}");

				// Example of using HookResult.Stop
				if (args.ConfigPath.Contains("restricted"))
				{
					Logger.LogWarning($"Access to restricted config blocked: {args.ConfigPath}");
					// Stop all processing - prevents the config load AND stops other handlers
					return HookResult.Stop;
				}

				return HookResult.Continue;
			}, HookMode.Pre);

			Events.Subscribe<ConfigLoadEventArgs>(EventType.ConfigLoad, args =>
			{
				Logger.LogInformation($"Config loaded for module: {args.ModuleName}");

				if (args.Config != null)
				{
					Logger.LogInformation($"Config contains {args.Config.Groups.Count} groups");
				}

				return HookResult.Continue;
			}, HookMode.Post);

			// Config save events
			Events.Subscribe<ConfigSaveEventArgs>(EventType.ConfigSave, args =>
			{
				Logger.LogInformation($"Config saving for module: {args.ModuleName}");
				return args.ConfigPath.Contains("important")
					? HookResult.Stop // Stop immediately if important config
					: HookResult.Continue; // Otherwise continue normally
			}, HookMode.Pre);

			Events.Subscribe<ConfigSaveEventArgs>(
				EventType.ConfigSave,
				args =>
				{
					Logger.LogInformation($"Config saved for module: {args.ModuleName}");
					return HookResult.Continue;
				},
				HookMode.Post
			);

			// Custom event examples
			RegisterCustomEvents();
			SubscribeToCustomEvents();

			// Simulate triggering a custom event
			SimulateCustomEvents();
		}

		// Register custom events that other plugins can subscribe to
		private void RegisterCustomEvents()
		{
			Logger.LogInformation("Registering custom events...");

			// Register a custom event for player actions
			bool playerActionRegistered = Events.RegisterCustom(CUSTOM_EVENT_PLAYER_ACTION);
			Logger.LogInformation($"Registered custom event '{CUSTOM_EVENT_PLAYER_ACTION}': {playerActionRegistered}");

			// Register a custom event for round end
			bool roundEndRegistered = Events.RegisterCustom(CUSTOM_EVENT_ROUND_END);
			Logger.LogInformation($"Registered custom event '{CUSTOM_EVENT_ROUND_END}': {roundEndRegistered}");

			// In a real plugin, you would document these events for other plugin developers
		}

		// Subscribe to custom events from other plugins
		private void SubscribeToCustomEvents()
		{
			Logger.LogInformation("Subscribing to custom events...");

			// Example: Subscribing to our own custom event to demonstrate the feature
			_playerActionSubscriptionId = Events.SubscribeCustom(CUSTOM_EVENT_PLAYER_ACTION, OnPlayerAction, HookMode.Post);
			Logger.LogInformation($"Subscribed to '{CUSTOM_EVENT_PLAYER_ACTION}' with ID: {_playerActionSubscriptionId}");

			// Example: Subscribe to an event with Pre hook to potentially block it
			var roundEndSubId = Events.SubscribeCustom(CUSTOM_EVENT_ROUND_END, OnRoundEnd, HookMode.Pre);
			Logger.LogInformation($"Subscribed to '{CUSTOM_EVENT_ROUND_END}' with ID: {roundEndSubId}");

			// In a real plugin, you would subscribe to events from other plugins
		}

		// Handler for player action custom event
		private HookResult OnPlayerAction(CustomEventArgs args)
		{
			Logger.LogInformation($"Custom event '{args.EventName}' triggered by plugin: {args.SourcePlugin}");

			// Access the custom event data
			if (args.Data is Dictionary<string, object> data)
			{
				if (data.TryGetValue("PlayerId", out var playerId) &&
					data.TryGetValue("Action", out var action))
				{
					Logger.LogInformation($"Player {playerId} performed action: {action}");
				}
			}

			return HookResult.Continue;
		}

		// Handler for round end custom event with Pre hook
		private HookResult OnRoundEnd(CustomEventArgs args)
		{
			Logger.LogInformation($"Pre-hook: Custom event '{args.EventName}' triggered by plugin: {args.SourcePlugin}");

			// Example: Conditionally block the event based on the data
			if (args.Data is Dictionary<string, object> data && data.TryGetValue("WinningTeam", out var winningTeam))
			{
				// Example: Block draws (in a real plugin, this would depend on your game logic)
				if (winningTeam.ToString() == "Draw")
				{
					Logger.LogWarning("Blocking round end with draw result");
					return HookResult.Handled;
				}
			}

			return HookResult.Continue;
		}

		// Simulate triggering custom events (for demonstration)
		private void SimulateCustomEvents()
		{
			Logger.LogInformation("Simulating custom events...");

			// Create some example data for the event
			var playerActionData = new Dictionary<string, object>
			{
				{ "PlayerId", 1 },
				{ "Action", "PlantBomb" },
				{ "Location", "BombsiteA" }
			};

			// Trigger the custom event
			bool eventTriggered = Events.TriggerCustom(CUSTOM_EVENT_PLAYER_ACTION, playerActionData);
			Logger.LogInformation($"Triggered '{CUSTOM_EVENT_PLAYER_ACTION}': {eventTriggered}");

			// Simulate a round end event with draw result (will be blocked by our Pre hook)
			var roundEndData = new Dictionary<string, object>
			{
				{ "WinningTeam", "Draw" },
				{ "Score", new Dictionary<string, int> { { "T", 15 }, { "CT", 15 } } }
			};

			bool roundEndTriggered = Events.TriggerCustom(CUSTOM_EVENT_ROUND_END, roundEndData);
			Logger.LogInformation($"Triggered '{CUSTOM_EVENT_ROUND_END}' (draw): {roundEndTriggered}");

			// Simulate a round end event with CT win (will not be blocked)
			var roundEndData2 = new Dictionary<string, object>
			{
				{ "WinningTeam", "CT" },
				{ "Score", new Dictionary<string, int> { { "T", 14 }, { "CT", 16 } } }
			};

			bool roundEndTriggered2 = Events.TriggerCustom(CUSTOM_EVENT_ROUND_END, roundEndData2);
			Logger.LogInformation($"Triggered '{CUSTOM_EVENT_ROUND_END}' (CT win): {roundEndTriggered2}");
		}

		// Player data save
		private HookResult OnPlayerDataSaving(PlayerDataEventArgs args)
		{
			Logger.LogInformation($"Player data saving for SteamID: {args.SteamId}");
			Logger.LogInformation($"Owner: {args.OwnerPlugin}");

			// Example: Block saving for specific players
			if (args.SteamId == 76561198043562034)
			{
				Logger.LogWarning($"Blocking data save for SteamID: {args.SteamId}");
				return HookResult.Handled;
			}

			return HookResult.Continue;
		}

		private HookResult OnPlayerDataSaved(PlayerDataEventArgs args)
		{
			Logger.LogInformation($"Player data saved for SteamID: {args.SteamId}");
			Logger.LogInformation($"Owner: {args.OwnerPlugin}");

			// Most post handlers will just return Continue
			return HookResult.Continue;
		}

		// Module events
		private HookResult OnModuleLoading(ModuleLoadEventArgs args)
		{
			Logger.LogInformation($"Module loading: {args.ModuleName}");

			// Example of using HookResult.Changed
			if (args.ModuleName.Contains("MyBlockedModule"))
			{
				Logger.LogWarning($"Module {args.ModuleName} contains restricted functionality");

				// Block loading of this module
				return HookResult.Handled;
			}
			else if (args.ModuleName.Contains("Debug"))
			{
				// Mark as changed to indicate we've modified something
				Logger.LogInformation("Debug module detected, special handling applied");
				return HookResult.Changed;
			}

			return HookResult.Continue;
		}

		private HookResult OnModuleLoaded(ModuleLoadEventArgs args)
		{
			Logger.LogInformation($"Module loaded: {args.ModuleName} v{args.Version}");
			Logger.LogInformation($"Module path: {args.ModulePath}");
			return HookResult.Continue;
		}

		protected override void SdkUnload(bool hotReload)
		{
			// Resource cleanup is automatically handled by SdkPlugin,
			// but you can unsubscribe from specific events if needed

			// ! Example: Manually unsubscribe from specific event, but the SdkPlugin will handle this automatically
			if (_playerActionSubscriptionId.HasValue)
			{
				Events.Unsubscribe(_playerActionSubscriptionId.Value);
				Logger.LogInformation($"Unsubscribed from '{CUSTOM_EVENT_PLAYER_ACTION}'");
			}
		}
	}
}