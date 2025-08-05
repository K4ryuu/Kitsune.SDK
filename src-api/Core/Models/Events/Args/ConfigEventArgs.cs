using Kitsune.SDK.Core.Models.Config;
using Kitsune.SDK.Core.Models.Events.Enums;

namespace Kitsune.SDK.Core.Models.Events.Args
{
    /// <summary>
    /// Base class for events related to configuration.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConfigEventArgs"/> class.
    /// </remarks>
    /// <param name="config">The module config.</param>
    /// <param name="moduleName">The name of the module.</param>
    public abstract class ConfigEventArgs(ModuleConfig config, string moduleName) : SdkEventArgs
    {
        /// <summary>
        /// Gets the module config that is being loaded or saved.
        /// </summary>
        public ModuleConfig Config { get; } = config;

        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        public string ModuleName { get; } = moduleName;
    }

    /// <summary>
    /// Arguments for config load events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConfigLoadEventArgs"/> class.
    /// </remarks>
    /// <param name="config">The module config being loaded.</param>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="configPath">The path to the configuration file.</param>
    public class ConfigLoadEventArgs(ModuleConfig config, string moduleName, string configPath) : ConfigEventArgs(config, moduleName)
    {
        /// <inheritdoc />
        public override EventType EventType => EventType.ConfigLoad;

        /// <summary>
        /// Gets the path to the configuration file.
        /// </summary>
        public string ConfigPath { get; } = configPath;
    }

    /// <summary>
    /// Arguments for config save events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConfigSaveEventArgs"/> class.
    /// </remarks>
    /// <param name="config">The module config being saved.</param>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="configPath">The path to the configuration file.</param>
    public class ConfigSaveEventArgs(ModuleConfig config, string moduleName, string configPath) : ConfigEventArgs(config, moduleName)
    {
        /// <inheritdoc />
        public override EventType EventType => EventType.ConfigSave;

        /// <summary>
        /// Gets the path to the configuration file.
        /// </summary>
        public string ConfigPath { get; } = configPath;
    }

    /// <summary>
    /// Arguments for config update events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConfigUpdateEventArgs"/> class.
    /// </remarks>
    /// <param name="moduleName">The name of the module that owns the configuration.</param>
    /// <param name="groupName">The group name of the updated config.</param>
    /// <param name="configName">The name of the updated config.</param>
    /// <param name="value">The new value of the config.</param>
    /// <param name="isGlobal">Whether this is a global config update.</param>
    public class ConfigUpdateEventArgs(string moduleName, string groupName, string configName, object? value, bool isGlobal = false) : SdkEventArgs
    {
        /// <inheritdoc />
        public override EventType EventType => EventType.Config;

        /// <summary>
        /// Gets the name of the module that owns the configuration.
        /// </summary>
        public string ModuleName { get; } = moduleName;

        /// <summary>
        /// Gets the group name of the updated config.
        /// </summary>
        public string GroupName { get; } = groupName;

        /// <summary>
        /// Gets the name of the updated config.
        /// </summary>
        public string ConfigName { get; } = configName;

        /// <summary>
        /// Gets the new value of the config.
        /// </summary>
        public object? Value { get; } = value;

        /// <summary>
        /// Gets whether this is a global config update.
        /// </summary>
        public bool IsGlobal { get; } = isGlobal;
    }
}