using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Player;
using Kitsune.SDK.Utilities;
using Microsoft.Extensions.Logging;

namespace ExampleProject
{
	/// <summary>
	/// Example plugin showing how to use the SDK CenterMessage system.
	/// </summary>
	[MinimumApiVersion(300)]
	[MinimumSdkVersion(1)]
	public class CenterMessageExample : SdkPlugin
	{
		public override string ModuleName => "CenterMessage Example";
		public override string ModuleVersion => "1.0.0";
		public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
		public override string ModuleDescription => "Demonstrates the usage of the SDK CenterMessage system";

		protected override void SdkLoad(bool hotReload)
		{
			Logger.LogInformation("CenterMessage Example plugin loaded!");
			
			// Inject the CenterMessage handler
			CenterMessageHandler.TryInject(this);
			
			// Register commands to demonstrate center messages
			Commands.Register("centermsg", "Show a center message", OnCenterMessageCommand);
			Commands.Register("prioritymsg", "Show a high priority center message", OnPriorityMessageCommand);
			Commands.Register("longmsg", "Show a long duration center message", OnLongMessageCommand);
		}

		protected override void SdkUnload(bool hotReload)
		{
			Logger.LogInformation("CenterMessage Example plugin unloaded!");
			
			// Uninject the CenterMessage handler
			CenterMessageHandler.TryUninject(this);
		}

		private void OnCenterMessageCommand(CCSPlayerController? player, CommandInfo commandInfo)
		{
			if (player == null || !player.IsValid)
				return;

			var sdkPlayer = Player.Find(player);
			if (sdkPlayer == null)
				return;

			string message = commandInfo.GetArg(1);
			if (string.IsNullOrEmpty(message))
			{
				message = "Hello from CenterMessage!";
			}

			// Show a normal priority message for 3 seconds
			sdkPlayer.PrintToCenter(message, 3, ActionPriority.Normal);
			
			Logger.LogInformation($"Sent center message to {player.PlayerName}: {message}");
		}

		private void OnPriorityMessageCommand(CCSPlayerController? player, CommandInfo commandInfo)
		{
			if (player == null || !player.IsValid)
				return;

			var sdkPlayer = Player.Find(player);
			if (sdkPlayer == null)
				return;

			string message = commandInfo.GetArg(1);
			if (string.IsNullOrEmpty(message))
			{
				message = "HIGH PRIORITY MESSAGE!";
			}

			// Show a high priority message for 5 seconds
			sdkPlayer.PrintToCenter($"<font color='#ff0000'><b>{message}</b></font>", 5, ActionPriority.High);
			
			Logger.LogInformation($"Sent priority center message to {player.PlayerName}: {message}");
		}

		private void OnLongMessageCommand(CCSPlayerController? player, CommandInfo commandInfo)
		{
			if (player == null || !player.IsValid)
				return;

			var sdkPlayer = Player.Find(player);
			if (sdkPlayer == null)
				return;

			string message = commandInfo.GetArg(1);
			if (string.IsNullOrEmpty(message))
			{
				message = "This is a long duration message!";
			}

			// Show a long duration message for 10 seconds
			sdkPlayer.PrintToCenter($"<font color='#00ff00'>{message}</font>", 10, ActionPriority.Normal);
			
			Logger.LogInformation($"Sent long center message to {player.PlayerName}: {message}");
		}
	}
}