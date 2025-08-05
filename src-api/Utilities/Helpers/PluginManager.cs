using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Plugin;
using CounterStrikeSharp.API.Core.Plugin.Host;
using System.Reflection;

namespace Kitsune.SDK.Utilities.Helpers
{
	/// <summary>
	/// Static helper class to interact with the CounterStrikeSharp plugin system
	/// Provides global access to plugin management functionality
	/// </summary>
	public static class PluginManager
	{
		// The plugin manager instance from CSS
		private static IPluginManager? _pluginManager;

		// Thread-safety lock
		private static readonly object _initLock = new();

		// Initialization flag
		private static bool _initialized = false;

		// Static constructor to ensure one-time initialization
		static PluginManager()
		{
			Initialize();
		}

		/// <summary>
		/// Thread-safe initialization of the plugin manager
		/// </summary>
		private static void Initialize()
		{
			if (_initialized)
				return;

			lock (_initLock)
			{
				if (_initialized)
					return;

				try
				{
					// Access the private _pluginManager field from CSS Application using reflection
					var field = typeof(Application).GetField("_pluginManager", BindingFlags.NonPublic | BindingFlags.Instance);
					_pluginManager = (IPluginManager?)field?.GetValue(Application.Instance);
					_initialized = _pluginManager != null;
				}
				catch
				{
					// Silently fail - will try again later if needed
				}
			}
		}

		/// <summary>
		/// Get the plugin manager instance
		/// </summary>
		public static IPluginManager? Instance
		{
			get
			{
				if (!_initialized || _pluginManager == null)
				{
					Initialize();
				}

				return _pluginManager;
			}
		}

		/// <summary>
		/// Get all loaded plugin contexts
		/// </summary>
		/// <returns>List of plugin instances</returns>
		public static IEnumerable<PluginContext> GetLoadedContexts()
		{
			if (!_initialized || _pluginManager == null)
			{
				Initialize();
			}

			if (_pluginManager == null)
				return [];

			try
			{
				return _pluginManager.GetLoadedPlugins().Where(context => context.State == PluginState.Loaded);
			}
			catch
			{
				return [];
			}
		}

		/// <summary>
		/// Check if a specific plugin context is loaded
		/// </summary>
		/// <param name="dllName">The name of the DLL to check</param>
		/// <returns>True if the context is loaded, false otherwise</returns>
		public static bool IsContextLoaded(string dllName)
		{
			if (!_initialized || _pluginManager == null)
			{
				Initialize();
			}

			if (_pluginManager == null)
				return false;

			try
			{
				return _pluginManager.GetLoadedPlugins().Any(context => Path.GetFileNameWithoutExtension(context.FilePath) == dllName && context.State == PluginState.Loaded);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Unload a specific plugin context
		/// </summary>
		/// <param name="dllName">The name of the DLL to unload</param>
		public static void UnloadContext(string dllName)
		{
			if (!_initialized || _pluginManager == null)
			{
				Initialize();
			}

			if (_pluginManager == null)
				return;

			try
			{
				var context = _pluginManager.GetLoadedPlugins().FirstOrDefault(c => Path.GetFileNameWithoutExtension(c.FilePath) == dllName);
				if (context != null)
				{
					context.Unload();
				}
			}
			catch
			{
				// Handle exceptions if needed
			}
		}
	}
}