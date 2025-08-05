using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Utilities.Helpers;

namespace Kitsune.Examples
{
	/// <summary>
	/// Example showing command handler functionality
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class CommandExample : SdkPlugin
	{
		public override string ModuleName => "Kitsune Command Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Example demonstrating command handler usage";

		protected override void SdkLoad(bool hotReload)
		{
			// ? When a command already exists by this plugin, the command will be overwritten
			// ? When a command already exists by another plugin, the registration will be denied and logged

			// Register basic command
			Commands.Register("hello", "Say hello", HandleHelloCommand);

			// Command with multiple aliases
			Commands.Register(["commandstatus", "commands", "cstatus"], "Gets the command status", CommandStatusCommand);

			// Command with optional arguments
			Commands.Register(
				"heal",
				"Heal yourself with a given amount",
				HealCommand,
				usage: CommandUsage.CLIENT_ONLY,
				argCount: 1,
				helpText: "<amount>",
				permission: "@kitsune/heal"
			);
		}

		private void HandleHelloCommand(CCSPlayerController? controller, CommandInfo info)
		{
			info.ReplyToCommand($" {ChatColors.Green}Hello, {controller?.PlayerName ?? "CONSOLE"}!");
		}

		private void CommandStatusCommand(CCSPlayerController? controller, CommandInfo info)
		{
			info.ReplyToCommand($" {ChatColors.Blue}Server Status:");
			info.ReplyToCommand($" {ChatColors.Blue}Players: {PlayerEx.GetValidPlayers().Count()}");
			info.ReplyToCommand($" {ChatColors.Blue}Commands: {Commands.GetCommands().Count}");
		}

		private void HealCommand(CCSPlayerController? controller, CommandInfo info)
		{
			if (controller == null) return; // Just to mute the warnings about nullability

			if (!int.TryParse(info.GetArg(1), out int amount))
			{
				info.ReplyToCommand($" {ChatColors.Red}Invalid amount: {info.GetArg(1)}");
				return;
			}

			if (controller.PlayerPawn.Value == null)
			{
				info.ReplyToCommand($" {ChatColors.Red}You are not a valid player.");
				return;
			}

			controller.PlayerPawn.Value.Health += amount;
			Utilities.SetStateChanged(controller, "CBaseEntity", "m_iHealth");

			info.ReplyToCommand($" {ChatColors.Green}You have been healed to {amount} health.");
		}

		protected override void SdkUnload(bool hotReload)
		{
			// ! Resource cleanup is automatically handled by SdkPlugin
		}
	}
}