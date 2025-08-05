using System.Collections.Concurrent;
using System.Text;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Models.Placeholders;
using Kitsune.SDK.Utilities;

namespace Kitsune.SDK.Services
{
	public sealed partial class PlaceholderHandler(BasePlugin plugin) : IDisposable
	{
		// One dictionary per placeholder type, with plugin info stored in the placeholder itself
		private static readonly ConcurrentDictionary<string, PlayerPlaceholder> _playerPlaceholders = new();
		private static readonly ConcurrentDictionary<string, ServerPlaceholder> _serverPlaceholders = new();

		private readonly BasePlugin _plugin = plugin;
		private readonly ILogger _logger = plugin.Logger;
		private bool _disposed;

		public void RegisterPlayer(string placeholder, Func<CCSPlayerController, string> callback)
		{
			StringEx.ValidatePlaceholder(placeholder, nameof(placeholder));

			// Check existing placeholder
			if (_playerPlaceholders.TryGetValue(placeholder, out var existing))
			{
				if (existing.Module != _plugin)
				{
					_logger.LogError($"Player placeholder '{placeholder}' is already registered by plugin '{existing.Module.ModuleName}'.");
					return;
				}

				_logger.LogWarning($"Player placeholder '{placeholder}' already exists for plugin '{_plugin.ModuleName}', overwriting.");
			}

			// Create and store placeholder
			_playerPlaceholders[placeholder] = new PlayerPlaceholder
			{
				Module = _plugin,
				Placeholder = placeholder,
				Callback = callback
			};
		}

		public void RegisterServer(string placeholder, Func<string> callback)
		{
			StringEx.ValidatePlaceholder(placeholder, nameof(placeholder));

			// Check existing placeholder
			if (_serverPlaceholders.TryGetValue(placeholder, out var existing))
			{
				if (existing.Module != _plugin)
				{
					_logger.LogError($"Server placeholder '{placeholder}' is already registered by plugin '{existing.Module.ModuleName}'.");
					return;
				}

				_logger.LogWarning($"Server placeholder '{placeholder}' already exists for plugin '{_plugin.ModuleName}', overwriting.");
			}

			// Create and store placeholder
			_serverPlaceholders[placeholder] = new ServerPlaceholder
			{
				Module = _plugin,
				Placeholder = placeholder,
				Callback = callback
			};
		}

		public void UnregisterPlayer(string placeholder)
		{
			if (!_playerPlaceholders.TryRemove(placeholder, out _))
			{
				_logger.LogWarning($"Failed to unregister player placeholder '{placeholder}' for plugin '{_plugin.ModuleName}'. Placeholder not found.");
			}
		}

		public void UnregisterServer(string placeholder)
		{
			if (!_serverPlaceholders.TryRemove(placeholder, out _))
			{
				_logger.LogWarning($"Failed to unregister server placeholder '{placeholder}' for plugin '{_plugin.ModuleName}'. Placeholder not found.");
			}
		}

		// Get plugin-specific placeholders
		public IReadOnlyList<PlayerPlaceholder> GetPlayerPlaceholders()
			=> [.. _playerPlaceholders.Values.Where(ph => ph.Module == _plugin)];

		public IReadOnlyList<ServerPlaceholder> GetServerPlaceholders()
			=> [.. _serverPlaceholders.Values.Where(ph => ph.Module == _plugin)];

		// Get all placeholders across all plugins
		public static IReadOnlyList<PlayerPlaceholder> GetAllPlayerPlaceholders()
			=> [.. _playerPlaceholders.Values];

		public static IReadOnlyList<ServerPlaceholder> GetAllServerPlaceholders()
			=> [.. _serverPlaceholders.Values];

		public void RemovePlaceholders()
		{
			// Find all placeholders belonging to this plugin
			var playerToRemove = _playerPlaceholders.Where(x => x.Value.Module == _plugin).Select(x => x.Key).ToArray();
			var serverToRemove = _serverPlaceholders.Where(x => x.Value.Module == _plugin).Select(x => x.Key).ToArray();

			// Remove them all
			foreach (var key in playerToRemove)
				_playerPlaceholders.TryRemove(key, out _);

			foreach (var key in serverToRemove)
				_serverPlaceholders.TryRemove(key, out _);
		}

		public static string ReplacePlayerPlaceholders(string text, CCSPlayerController player)
		{
			// Fast early return if no placeholders to process or no braces in text
			if (_playerPlaceholders.IsEmpty || !text.Contains('{'))
				return text;

			var sb = new StringBuilder(text);

			// Avoid checking all placeholders by only trying to replace those in the text
			foreach (var ph in _playerPlaceholders.Values)
			{
				if (!text.Contains(ph.Placeholder))
					continue;

				try
				{
					var value = ph.Callback(player);
					sb.Replace(ph.Placeholder, value);
				}
				catch
				{
					// Replace with N/A when placeholder execution fails
					sb.Replace(ph.Placeholder, "N/A");
				}
			}

			return sb.ToString();
		}

		public static string ReplaceServerPlaceholders(string text)
		{
			// Fast early return if no placeholders to process or no braces in text
			if (_serverPlaceholders.IsEmpty || !text.Contains('{'))
				return text;

			var sb = new StringBuilder(text);

			// Avoid checking all placeholders by only trying to replace those in the text
			foreach (var ph in _serverPlaceholders.Values)
			{
				if (!text.Contains(ph.Placeholder))
					continue;

				try
				{
					var value = ph.Callback();
					sb.Replace(ph.Placeholder, value);
				}
				catch
				{
					// Replace with N/A when placeholder execution fails
					sb.Replace(ph.Placeholder, "N/A");
				}
			}

			return sb.ToString();
		}

		public static string ReplaceAll(string text, CCSPlayerController? player = null)
		{
			// Skip processing if text is empty or has no placeholders
			if (string.IsNullOrEmpty(text) || !text.Contains('{'))
				return text;

			// Process player placeholders if player is valid
			if (player?.IsValid == true)
				text = ReplacePlayerPlaceholders(text, player);

			// Process server placeholders
			return ReplaceServerPlaceholders(text);
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			// Clean up all placeholders owned by this plugin
			RemovePlaceholders();
			_disposed = true;
			GC.SuppressFinalize(this);
		}
	}
}