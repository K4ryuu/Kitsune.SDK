using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Services.Data.Settings;
using Kitsune.SDK.Services.Data.Storage;
using Kitsune.SDK.Services.Data.Base;

namespace Kitsune.SDK.Core.Base
{
	/// <summary>
	/// Abstract class for player entity that provides integrated access to storage and settings
	/// </summary>
	public partial class Player
	{
		/// <summary>
		/// Flag to control when Player constructor can be called
		/// </summary>
		private static bool _allowConstruction = false;

		/// <summary>
		/// Static list of all players indexed by SteamID
		/// </summary>
		public static ConcurrentDictionary<ulong, Player> List { get; } = new();

		/// <summary>
		/// Dictionary mapping keys to their originating plugin/handler
		/// </summary>
		private static readonly ConcurrentDictionary<string, BasePlugin> _keyToPlugin = new();

		/// <summary>
		/// Cache for plugin name to BasePlugin mappings (shared for all extensions)
		/// </summary>
		internal static readonly ConcurrentDictionary<string, BasePlugin> PluginNameCache = new();

		/// <summary>
		/// Cleanup key sources for a plugin
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CleanupPluginKeys(BasePlugin plugin)
		{
			if (plugin == null)
				return;

			// Use a single list to minimize allocations
			var keysToRemove = new List<string>(_keyToPlugin.Count / 4);

			// Collect all keys to remove
			foreach (var kvp in _keyToPlugin)
			{
				if (kvp.Value == plugin)
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			// Remove in batch
			foreach (var key in keysToRemove)
			{
				_keyToPlugin.TryRemove(key, out _);
			}

			// Clear from plugin name cache - usually just 1-2 entries
			keysToRemove.Clear();
			foreach (var kvp in PluginNameCache)
			{
				if (kvp.Value == plugin)
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			foreach (var name in keysToRemove)
			{
				PluginNameCache.TryRemove(name, out _);
			}
		}

		/// <summary>
		/// Lock for thread-safe player creation
		/// </summary>
		private static readonly object _getOrCreateLock = new();

		/// <summary>
		/// Lock object for thread-safe disposal
		/// </summary>
		private readonly object _disposeLock = new();

		/// <summary>
		/// Flag indicating if the player has been disposed
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Player's controller
		/// </summary>
		public readonly CCSPlayerController Controller;

		/// <summary>
		/// Player's SteamID
		/// </summary>
		public readonly ulong SteamID;

		/// <summary>
		/// Player's name
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// Is the player fully loaded
		/// </summary>
		public bool IsLoaded { get; private set; } = false;

		/// <summary>
		/// Constructor for Player instances - should only be called through GetOrCreate.
		/// Public but protected by runtime validation to ensure only SDK can create instances.
		/// </summary>
		public Player(CCSPlayerController controller, bool skipDataLoad = false)
		{
			// Check if construction is allowed
			if (!_allowConstruction)
				throw new InvalidOperationException("Player instances can only be created by the SDK through GetOrCreate methods");

			if (controller == null || !controller.IsValid)
				throw new ArgumentNullException(nameof(controller), "Player controller cannot be null or invalid");

			// Initialize basic properties
			Controller = controller;
			SteamID = controller.SteamID;
			Name = controller.PlayerName ?? "Unknown";

			// Add to player list
			List[SteamID] = this;

			// Only load data if not skipped (for hot reload scenarios)
			if (!skipDataLoad)
			{
				// Load player data efficiently using the simplified unified loading
				CSSThread.RunOnMainThread(async () =>
				{
					try
					{
						// Use the simplified unified loading
						await PlayerDataHandler.LoadPlayerDataUnifiedAsync(SteamID);

						// Set the loaded flag to true BEFORE firing the event
						IsLoaded = true;

						// Now fire the PlayerDataLoad event when player is fully loaded
						PlayerDataHandler.FireUnifiedPlayerDataLoadEvent(SteamID);
					}
					catch (Exception ex)
					{
						throw new InvalidOperationException($"Error loading data for {Name} ({SteamID}): {ex.Message}", ex);
					}
				});
			}
			else
			{
				// Data will be loaded externally, just mark as loaded
				IsLoaded = true;
			}
		}

		/// <summary>
		/// Find a player by controller
		/// </summary>
		/// <param name="controller">The player controller to find</param>
		/// <returns>The player instance if found and valid, otherwise null</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Player? Find(CCSPlayerController? controller)
		{
			if (controller == null || !controller.IsValid)
				return null;

			if (List.TryGetValue(controller.SteamID, out var player) && player?.IsValid == true)
				return player;

			// Remove invalid player reference
			if (player != null)
				List.TryRemove(controller.SteamID, out _);

			return null;
		}

		/// <summary>
		/// Find a player by SteamID
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Player? Find(ulong steamid)
		{
			if (List.TryGetValue(steamid, out var player) && player?.IsValid == true)
				return player;

			// Remove invalid player reference
			if (player != null)
				List.TryRemove(steamid, out _);

			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<Player> ValidLoop()
		{
			// Use a fast path to avoid allocations
			foreach (var player in List.Values)
			{
				if (player.IsValid == true)
				{
					yield return player;
				}
				else
				{
					// Remove invalid player reference
					List.TryRemove(player.SteamID, out _);
				}
			}
		}

		/// <summary>
		/// Gets an existing player or creates a new one if not exists.
		/// This is the preferred way to get or create player instances.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? GetOrCreate<T>(CCSPlayerController? controller, bool skipDataLoad = false) where T : Player
		{
			if (controller == null || !controller.IsValid || controller.IsBot || controller.IsHLTV)
				return null;

			var steamId = controller.SteamID;

			// Fast path: Check if player already exists without locking
			if (List.TryGetValue(steamId, out var existingPlayer) && existingPlayer?.IsValid == true)
			{
				// If it's the requested type, return it
				if (existingPlayer is T typedPlayer)
					return typedPlayer;

				// Try to cast to the requested type (for inheritance cases)
				if (existingPlayer.GetType().IsAssignableTo(typeof(T)))
					return existingPlayer as T;

				// Type mismatch - return null
				return null;
			}

			// Slow path: Need to create or re-check with lock
			lock (_getOrCreateLock)
			{
				// Double-check pattern: Check again under lock
				if (List.TryGetValue(steamId, out existingPlayer) && existingPlayer?.IsValid == true)
				{
					// If it's the requested type, return it
					if (existingPlayer is T typedPlayer)
						return typedPlayer;

					// Try to cast to the requested type (for inheritance cases)
					if (existingPlayer.GetType().IsAssignableTo(typeof(T)))
						return existingPlayer as T;

					// Type mismatch - return null
					return null;
				}
				else if (existingPlayer != null)
				{
					// Remove invalid player reference
					List.TryRemove(steamId, out _);
				}

				try
				{
					// Allow construction temporarily
					_allowConstruction = true;

					// Create new player instance
					var newPlayer = (T)Activator.CreateInstance(typeof(T), [controller, skipDataLoad])!;

					// Disable construction again
					_allowConstruction = false;

					return newPlayer;
				}
				catch (Exception ex)
				{
					// Make sure to reset the flag even if construction fails
					_allowConstruction = false;

					// Log the actual error for debugging
					throw new InvalidOperationException($"Failed to create player instance: {ex.Message}", ex);
				}
			}
		}

		/// <summary>
		/// Register a key with its source plugin
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void RegisterKeySource(string fullKey, BasePlugin plugin, bool isStorage)
		{
			if (string.IsNullOrEmpty(fullKey) || plugin == null)
				return;

			_keyToPlugin[fullKey] = plugin;
		}

		/// <summary>
		/// Is the player valid (alive, connected)
		/// </summary>
		public bool IsValid
			=> Controller.IsValid && Controller.PlayerPawn?.IsValid == true;

		/// <summary>
		/// Is the player a bot
		/// </summary>
		public bool IsBot
			=> Controller.IsBot || Controller.IsHLTV;

		/// <summary>
		/// Is the player alive
		/// </summary>
		public bool IsAlive
			=> Controller.LifeState == (byte)LifeState_t.LIFE_ALIVE;

		/// <summary>
		/// Thread-safe disposal that handles multiple calls from different plugins
		/// </summary>
		public void Dispose()
		{
			lock (_disposeLock)
			{
				// Only perform actual disposal on the first call
				if (_disposed)
					return;

				try
				{

					// Get all handlers from PlayerDataHandler registry
					var allHandlers = PlayerDataHandler.GetAllHandlers().ToList();

					// Batch save all handler data in a single transaction
					try
					{
						// Use Task.Run to avoid deadlock on main thread
						Task.Run(async () => await PlayerDataHandler.SaveAllPlayerDataBatchAsync(SteamID, allHandlers)).Wait();
					}
					catch (Exception ex)
					{
						throw new Exception($"Error during batch save for {Name} ({SteamID}): {ex.Message}", ex);
					}

					// Set disposed flag AFTER batch save is complete
					_disposed = true;

					// Clear player data from all handlers AFTER saving
					foreach (var handler in allHandlers)
					{
						try
						{
							// Clear only the cache, don't save again
							var cache = PlayerDataHandler.GetPlayerCacheStatic(SteamID, handler.HandlerDataType);
							var keysToRemove = cache.Keys.Where(key => key.StartsWith($"{handler.OwnerPlugin}:", StringComparison.Ordinal)).ToList();

							foreach (var key in keysToRemove)
							{
								cache.TryRemove(key, out _);
							}

							if (handler is StorageHandler storageHandler)
								storageHandler.ClearOriginalValues(SteamID);

						}
						catch (ObjectDisposedException)
						{
							// Handler is already disposed, skip cleanup for this handler
						}
					}

					SettingsHandler.CleanupPlayerInstances(SteamID);
					StorageHandler.CleanupPlayerInstances(SteamID);

					// Remove from player list
					List.TryRemove(SteamID, out _);
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException($"Error disposing player {Name} ({SteamID}): {ex.Message}", ex);
				}
			}
		}

		/// <summary>
		/// Ensures disposal happens on garbage collection
		/// </summary>
		~Player()
		{
			if (!_disposed)
				Dispose();
		}
	}
}