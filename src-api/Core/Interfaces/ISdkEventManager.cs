using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Core.Models.Events.Enums;

namespace Kitsune.SDK.Core.Models.Events.Args
{
    /// <summary>
    /// Interface for the SDK event manager.
    /// </summary>
    public interface ISdkEventManager
    {
        /// <summary>
        /// Subscribes to an event with the specified hook mode using a callback that returns a HookResult.
        /// </summary>
        /// <param name="eventType">The type of event to subscribe to.</param>
        /// <param name="callback">The callback to invoke when the event is triggered, returning a HookResult.</param>
        /// <param name="hookMode">When the event should be triggered. Defaults to Post.</param>
        /// <returns>An identifier that can be used to unsubscribe from the event.</returns>
        Guid Subscribe(EventType eventType, Func<SdkEventArgs, HookResult> callback, HookMode hookMode = HookMode.Post);

        /// <summary>
        /// Subscribes to an event with the specified hook mode using a callback that returns a HookResult.
        /// </summary>
        /// <typeparam name="T">The type of event arguments.</typeparam>
        /// <param name="eventType">The type of event to subscribe to.</param>
        /// <param name="callback">The callback to invoke when the event is triggered, returning a HookResult.</param>
        /// <param name="hookMode">When the event should be triggered. Defaults to Post.</param>
        /// <returns>An identifier that can be used to unsubscribe from the event.</returns>
        Guid Subscribe<T>(EventType eventType, Func<T, HookResult> callback, HookMode hookMode = HookMode.Post) where T : SdkEventArgs;

        /// <summary>
        /// Unsubscribes from an event.
        /// </summary>
        /// <param name="subscriptionId">The identifier returned by the Subscribe method.</param>
        /// <returns>True if the subscription was removed; otherwise, false.</returns>
        bool Unsubscribe(Guid subscriptionId);

        /// <summary>
        /// Dispatches an event to all subscribers.
        /// </summary>
        /// <param name="eventArgs">The event arguments.</param>
        /// <param name="hookMode">The hook mode to dispatch to.</param>
        /// <returns>True if the event was not blocked; otherwise, false.</returns>
        bool Dispatch(SdkEventArgs eventArgs, HookMode hookMode);

        /// <summary>
        /// Registers a custom event that can be triggered by plugins.
        /// </summary>
        /// <param name="eventName">The unique name of the custom event.</param>
        /// <param name="sourcePlugin">The name of the plugin registering the event.</param>
        /// <returns>True if the event was registered successfully; otherwise, false.</returns>
        bool RegisterCustom(string eventName);

        /// <summary>
        /// Subscribes to a custom event with the specified hook mode using a callback that returns a HookResult.
        /// </summary>
        /// <param name="eventName">The name of the custom event to subscribe to.</param>
        /// <param name="callback">The callback to invoke when the event is triggered, returning a HookResult.</param>
        /// <param name="hookMode">When the event should be triggered. Defaults to Post.</param>
        /// <returns>An identifier that can be used to unsubscribe from the event.</returns>
        Guid SubscribeCustom(string eventName, Func<CustomEventArgs, HookResult> callback, HookMode hookMode = HookMode.Post);

        /// <summary>
        /// Triggers a custom event with the specified name and data.
        /// </summary>
        /// <param name="eventName">The name of the custom event to trigger.</param>
        /// <param name="sourcePlugin">The name of the plugin triggering the event.</param>
        /// <param name="data">Optional data to be passed with the event.</param>
        /// <returns>True if the event was not blocked; otherwise, false.</returns>
        bool TriggerCustom(string eventName, object? data = null);
    }
}