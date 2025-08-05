using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Core.Models.Events.Enums;
using Microsoft.Extensions.Logging;

using Player = Kitsune.SDK.Core.Base.Player;

namespace Kitsune.SDK.Utilities
{
	/// <summary>
	/// Global center message handler that manages showing center messages for all players
	/// </summary>
	public static class CenterMessageHandler
	{
		private static readonly List<SdkPlugin> _registeredPlugins = [];
		private static SdkPlugin? _activePlugin = null;

		/// <summary>
		/// Synchronous version of TryInjectAsync
		/// </summary>
		public static void TryInject(SdkPlugin plugin)
		{
			try
			{
				// Add plugin to registered list if not already present
				if (!_registeredPlugins.Contains(plugin))
				{
					_registeredPlugins.Add(plugin);

					Server.NextWorldUpdate(() =>
					{
						// Subscribe to plugin unload events to handle automatic cleanup
						plugin.Events.Subscribe<ModuleUnloadEventArgs>(EventType.ModuleUnload, args =>
						{
							if (args.ModuleName == plugin.ModuleName)
							{
								TryUninject(plugin);
							}

							return HookResult.Continue;
						}, HookMode.Pre);
					});
				}

				// If no active plugin, make this one active
				if (_activePlugin == null)
				{
					Server.NextWorldUpdate(() => SetActivePlugin(plugin));
				}
			}
			catch (Exception ex)
			{
				plugin.Logger.LogError(ex, "CenterMessageHandler: Failed to inject");
			}
		}

		/// <summary>
		/// Remove a plugin from the center message handler
		/// </summary>
		public static void TryUninject(SdkPlugin plugin)
		{
			// Remove plugin from registered list
			_registeredPlugins.Remove(plugin);

			// If this was the active plugin, find a replacement
			if (_activePlugin == plugin)
			{
				UnhookFromActivePlugin();
				_activePlugin = null;

				// Try to activate another registered plugin
				if (_registeredPlugins.Count > 0)
				{
					SetActivePlugin(_registeredPlugins[0]);
				}
			}
		}

		/// <summary>
		/// Set the active plugin for center message handling
		/// </summary>
		private static void SetActivePlugin(SdkPlugin plugin)
		{
			_activePlugin = plugin;
			plugin.RegisterListener<Listeners.OnTick>(UpdateCenterMessages);
		}

		/// <summary>
		/// Remove hooks from the active plugin
		/// </summary>
		private static void UnhookFromActivePlugin()
		{
			if (_activePlugin != null)
			{
				try
				{
					_activePlugin.RemoveListener<Listeners.OnTick>(UpdateCenterMessages);
				}
				catch
				{
					// Ignore unhook errors during cleanup
				}
			}
		}

		/// <summary>
		/// Get the currently active plugin
		/// </summary>
		public static SdkPlugin? GetActivePlugin()
		{
			return _activePlugin;
		}

		/// <summary>
		/// Get all registered plugins
		/// </summary>
		public static IReadOnlyList<SdkPlugin> GetRegisteredPlugins()
		{
			return [.. _registeredPlugins];
		}

		/// <summary>
		/// Update center messages for all valid players
		/// </summary>
		private static void UpdateCenterMessages()
		{
			foreach (var player in Player.ValidLoop())
			{
				player.ShowCenterMessage();
			}
		}
	}
}