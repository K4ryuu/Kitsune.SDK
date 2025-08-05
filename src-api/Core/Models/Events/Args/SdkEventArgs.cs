using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Core.Models.Events.Enums;

namespace Kitsune.SDK.Core.Models.Events
{
    /// <summary>
    /// Base class for all SDK event arguments.
    /// </summary>
    public abstract class SdkEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the type of event.
        /// </summary>
        public abstract EventType EventType { get; }

        /// <summary>
        /// Gets or sets the hook result for this event.
        /// - HookResult.Continue: Continue processing the hook to other listeners.
        /// - HookResult.Changed: The hook result has been changed.
        /// - HookResult.Handled: The hook has been handled. The original method won't be called, but other hooks will still be called.
        /// - HookResult.Stop: Stop processing the hook. The original method won't be called, and other hooks will not proceed.
        /// </summary>
        public HookResult Result { get; set; } = HookResult.Continue;
    }
}