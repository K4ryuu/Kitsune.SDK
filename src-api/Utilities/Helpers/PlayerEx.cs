using CounterStrikeSharp.API.Core;

namespace Kitsune.SDK.Utilities.Helpers
{
    public static class PlayerEx
    {
        /// <summary>
        /// Get all valid (non-bot, non-HLTV) players currently on the server
        /// </summary>
        /// <returns>Enumerable collection of valid players</returns>
        public static IEnumerable<CCSPlayerController> GetValidPlayers()
        {
            var players = CounterStrikeSharp.API.Utilities.GetPlayers();

            foreach (var player in players)
            {
                if (player.IsBot || player.IsHLTV)
                    continue;

                yield return player;
            }
        }
    }
}