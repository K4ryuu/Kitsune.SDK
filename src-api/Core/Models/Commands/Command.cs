using CounterStrikeSharp.API.Core.Commands;
using CounterStrikeSharp.API.Modules.Commands;
using Kitsune.SDK.Core.Base;

namespace Kitsune.SDK.Core.Models.Commands
{
	/// <summary>
	/// Represents a command that can be registered with the server
	/// </summary>
	public class Command
	{
		/// <summary>
		/// The module that registered this command
		/// </summary>
		public required SdkPlugin Module;

		/// <summary>
		/// The command text (without !)
		/// </summary>
		public required string CommandText;

		/// <summary>
		/// A short description of what the command does
		/// </summary>
		public required string Description;

		/// <summary>
		/// The callback function that is executed when the command is used
		/// </summary>
		public required CommandInfo.CommandCallback Callback;

		/// <summary>
		/// Where the command can be used (client, server, or both)
		/// </summary>
		public CommandUsage Usage = CommandUsage.CLIENT_AND_SERVER;

		/// <summary>
		/// The minimum number of arguments required
		/// </summary>
		public int ArgCount = 0;

		/// <summary>
		/// Optional help text to display when the command is used incorrectly
		/// </summary>
		public string? HelpText = null;

		/// <summary>
		/// Optional permission required to use the command
		/// </summary>
		public string? Permission = null;

		/// <summary>
		/// The command definition object created when the command is registered
		/// </summary>
		public CommandDefinition? CommandDefinition { get; set; }
	}
}