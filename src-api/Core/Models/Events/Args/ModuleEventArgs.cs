using Kitsune.SDK.Core.Models.Events.Enums;

namespace Kitsune.SDK.Core.Models.Events.Args
{
    /// <summary>
    /// Base class for events related to modules.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ModuleEventArgs"/> class.
    /// </remarks>
    /// <param name="moduleName">The name of the module.</param>
    public abstract class ModuleEventArgs(string moduleName) : SdkEventArgs
    {
        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        public string ModuleName { get; } = moduleName;
    }

    /// <summary>
    /// Arguments for module load events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ModuleLoadEventArgs"/> class.
    /// </remarks>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="version">The version of the module.</param>
    /// <param name="modulePath">The path to the module assembly.</param>
    public class ModuleLoadEventArgs(string moduleName, string version, string modulePath) : ModuleEventArgs(moduleName)
    {
        /// <inheritdoc />
        public override EventType EventType => EventType.ModuleLoad;

        /// <summary>
        /// Gets the version of the module.
        /// </summary>
        public string Version { get; } = version;

        /// <summary>
        /// Gets the path to the module assembly.
        /// </summary>
        public string ModulePath { get; } = modulePath;
    }

    /// <summary>
    /// Arguments for module unload events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ModuleUnloadEventArgs"/> class.
    /// </remarks>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="reason">The reason for the module being unloaded.</param>
    public class ModuleUnloadEventArgs(string moduleName, string reason) : ModuleEventArgs(moduleName)
    {
        /// <inheritdoc />
        public override EventType EventType => EventType.ModuleUnload;

        /// <summary>
        /// Gets the reason for the module being unloaded.
        /// </summary>
        public string Reason { get; } = reason;
    }
}