using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Services.Data.Base;

namespace Kitsune.SDK.Core.Models.Data
{
	public class PlayerOperationInfo
	{
		public ulong SteamId { get; set; }
		public PlayerDataHandler Handler { get; set; } = null!;
		public BasePlugin Plugin { get; set; } = null!;
	}
}