using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Core.Translations;
using Kitsune.SDK.Core.Models.Commands;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Utilities;

namespace Kitsune.SDK.Services.Commands
{
	/// <summary>
	/// Handles command registration and execution for Zenith plugins
	/// </summary>
	public sealed partial class CommandHandler(SdkPlugin plugin) : IDisposable
	{
		// Simplified data storage - use ConcurrentDictionary for thread safety without extra locks
		private static readonly ConcurrentDictionary<string, CommandRegistration> _globalCommands = new();

		// Simplified command registration info
		private class CommandRegistration
		{
			public required SdkPlugin Owner { get; init; }
			public required Command Command { get; init; }
		}

		// Fields initialized with primary constructor syntax
		private readonly SdkPlugin _plugin = plugin;
		private static IStringLocalizer Localizer => SdkTranslations.Instance;
		private readonly ILogger _logger = plugin.Logger;
		private bool _disposed;

		public void Register(string command, string description, CommandInfo.CommandCallback handler, CommandUsage usage = CommandUsage.CLIENT_AND_SERVER, int argCount = 0, string? helpText = null, string? permission = null)
		{
			StringEx.ValidateName(command, nameof(command));
			command = command.StartsWith("css_") ? command : "css_" + command;

			if (_globalCommands.TryGetValue(command, out var existing))
			{
				_logger.LogError($"Command '{command}' is already registered by plugin '{existing.Owner.ModuleName}'.");
				return;
			}

			// Create command definition with validation wrapper
			var newCommand = new CommandDefinition(command, description, (controller, info) =>
			{
				// Simplified validation in one step
				if (!ValidateCommand(controller, info, usage, argCount, helpText, permission))
					return;

				// Profile command execution when profiling is enabled
				// Command execution is automatically profiled if the plugin class has profiling attributes
				handler(controller, info);
			});

			_plugin.CommandManager.RegisterCommand(newCommand);

			// Create and store Command object
			var cmd = new Command
			{
				Module = _plugin,
				CommandText = command,
				Description = description,
				Callback = handler,
				Usage = usage,
				ArgCount = argCount,
				HelpText = helpText,
				Permission = permission,
				CommandDefinition = newCommand
			};

			// Store command in global registry
			_globalCommands[command] = new CommandRegistration { Owner = _plugin, Command = cmd };
		}

		public void Register(List<string> commands, string description, CommandInfo.CommandCallback handler, CommandUsage usage = CommandUsage.CLIENT_AND_SERVER, int argCount = 0, string? helpText = null, string? permission = null)
		{
			foreach (var command in commands)
			{
				Register(command, description, handler, usage, argCount, helpText, permission);
			}
		}

		public IReadOnlyCollection<Command> GetCommands()
			=> [.. _globalCommands.Values.Where(reg => reg.Owner == _plugin).Select(reg => reg.Command)];

		public static IReadOnlyList<Command> GetAllCommands()
			=> [.. _globalCommands.Values.Select(reg => reg.Command)];

		// Consolidated validation logic in one method
		private static bool ValidateCommand(CCSPlayerController? player, CommandInfo info, CommandUsage usage, int argCount, string? helpText, string? permission)
		{
			// Usage validation
			if (usage == CommandUsage.CLIENT_ONLY && (player == null || !player.IsValid))
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "kitsune.sdk.general.prefix")} {Localizer.ForPlayer(player, "kitsune.sdk.command.client-only")}");
				return false;
			}

			if (usage == CommandUsage.SERVER_ONLY && player != null)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "kitsune.sdk.general.prefix")} {Localizer.ForPlayer(player, "kitsune.sdk.command.server-only")}");
				return false;
			}

			// Permission validation
			if (!string.IsNullOrEmpty(permission))
			{
				bool hasPermission = AdminManager.PlayerHasPermissions(player, "@css/root") || AdminManager.PlayerHasPermissions(player, permission) || AdminManager.GetPlayerCommandOverrideState(player, info.GetArg(0));

				if (!hasPermission)
				{
					info.ReplyToCommand($" {Localizer.ForPlayer(player, "kitsune.sdk.general.prefix")} {Localizer.ForPlayer(player, "kitsune.sdk.command.no-permission")}");
					return false;
				}
			}

			// Argument count validation
			if (argCount > 0 && info.ArgCount < argCount + 1 && helpText != null)
			{
				info.ReplyToCommand($" {Localizer.ForPlayer(player, "kitsune.sdk.general.prefix")} {Localizer.ForPlayer(player, "kitsune.sdk.command.help", info.ArgByIndex(0), helpText)}");
				return false;
			}

			return true;
		}

		private void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				// Clean up all commands from this plugin in one batch
				var commandKeys = _globalCommands.Where(kvp => kvp.Value.Owner == _plugin).Select(kvp => kvp.Key).ToArray();

				foreach (var key in commandKeys)
				{
					if (_globalCommands.TryRemove(key, out var registration))
					{
						try
						{
							if (registration.Command.CommandDefinition != null)
							{
								_plugin.CommandManager.RemoveCommand(registration.Command.CommandDefinition);
							}
						}
						catch { /* Ignore errors during cleanup */ }
					}
				}
			}

			_disposed = true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~CommandHandler()
		{
			Dispose(false);
		}
	}
}