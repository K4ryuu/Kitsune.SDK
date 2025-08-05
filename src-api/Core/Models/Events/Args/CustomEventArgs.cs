using Kitsune.SDK.Core.Models.Events.Enums;

namespace Kitsune.SDK.Core.Models.Events.Args
{
    /// <summary>
    /// Custom event arguments to support plugin-defined events.
    /// Plugins can use this class to define their own custom events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CustomEventArgs"/> class.
    /// </remarks>
    /// <param name="eventName">The unique name of the custom event.</param>
    /// <param name="sourcePlugin">The name of the plugin that registered the event.</param>
    /// <param name="data">Optional data to be passed with the event.</param>
    public class CustomEventArgs(string eventName, string sourcePlugin, object? data = null) : SdkEventArgs
    {
        /// <summary>
        /// Gets or sets the unique name of the custom event.
        /// This is used to identify the custom event type.
        /// </summary>
        public string EventName { get; } = eventName ?? throw new ArgumentNullException(nameof(eventName));

        /// <summary>
        /// Gets or sets the plugin that registered the custom event.
        /// </summary>
        public string SourcePlugin { get; } = sourcePlugin ?? throw new ArgumentNullException(nameof(sourcePlugin));

        /// <summary>
        /// Gets or sets custom data associated with the event.
        /// </summary>
        public object? Data { get; } = data;

        /// <summary>
        /// Gets the event type.
        /// </summary>
        public override EventType EventType { get; } = EventType.Custom;
    }
}