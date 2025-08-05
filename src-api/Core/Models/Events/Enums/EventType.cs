namespace Kitsune.SDK.Core.Models.Events.Enums
{
    /// <summary>
    /// Defines the types of events that can be dispatched by the SDK.
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Event related to player data load operation.
        /// Use with HookMode.Pre to execute before loading, or HookMode.Post to execute after loading.
        /// </summary>
        PlayerDataLoad,

        /// <summary>
        /// Event related to player data save operation.
        /// Use with HookMode.Pre to execute before saving, or HookMode.Post to execute after saving.
        /// </summary>
        PlayerDataSave,

        /// <summary>
        /// Event related to module loading.
        /// Use with HookMode.Pre to execute before loading, or HookMode.Post to execute after loading.
        /// </summary>
        ModuleLoad,

        /// <summary>
        /// Event related to module unloading.
        /// Use with HookMode.Pre to execute before unloading, or HookMode.Post to execute after unloading.
        /// </summary>
        ModuleUnload,

        /// <summary>
        /// Event related to config loading.
        /// Use with HookMode.Pre to execute before loading, or HookMode.Post to execute after loading.
        /// </summary>
        ConfigLoad,

        /// <summary>
        /// Event related to config saving.
        /// Use with HookMode.Pre to execute before saving, or HookMode.Post to execute after saving.
        /// </summary>
        ConfigSave,

        /// <summary>
        /// Event related to config value updates.
        /// Use with HookMode.Pre to execute before updating, or HookMode.Post to execute after updating.
        /// </summary>
        Config,

        /// <summary>
        /// Custom event type for plugin-defined events.
        /// Use with CustomEventArgs to define plugin-specific events.
        /// </summary>
        Custom
    }
}