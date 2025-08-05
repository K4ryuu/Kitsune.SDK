using Kitsune.SDK.Core.Models.Events.Enums;

namespace Kitsune.SDK.Core.Models.Events.Args
{
    /// <summary>
    /// Event arguments for player data events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PlayerDataEventArgs"/> class.
    /// </remarks>
    /// <param name="steamId">The SteamID of the player.</param>
    /// <param name="dataType">The type of data being processed.</param>
    /// <param name="ownerPlugin">The owner plugin identifier.</param>
    /// <param name="eventType">The event type.</param>
    public class PlayerDataEventArgs(ulong steamId, string ownerPlugin, EventType eventType) : SdkEventArgs
    {
        /// <summary>
        /// Gets the SteamID of the player.
        /// </summary>
        public ulong SteamId { get; } = steamId;

        /// <summary>
        /// Gets the owner plugin identifier.
        /// </summary>
        public string OwnerPlugin { get; } = ownerPlugin;

        /// <summary>
        /// Gets the event type.
        /// </summary>
        public override EventType EventType { get; } = eventType;
    }
}