using Kitsune.SDK.Core.Models.Config;

namespace Kitsune.SDK.Core.Attributes.Config
{
    /// <summary>
    /// Attribute for marking configuration properties
    /// </summary>
    /// <remarks>
    /// Creates a new configuration attribute
    /// </remarks>
    /// <param name="name">The name of the configuration item</param>
    /// <param name="description">The description of the configuration item</param>
    /// <remarks>
    /// Creates a new configuration attribute
    /// </remarks>
    /// <param name="name">The name of the configuration item</param>
    /// <param name="description">The description of the configuration item</param>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ConfigAttribute(string name, string description) : Attribute
    {
        /// <summary>
        /// Creates a new configuration attribute with flags
        /// </summary>
        /// <param name="name">The name of the configuration item</param>
        /// <param name="description">The description of the configuration item</param>
        /// <param name="flags">The flags for the configuration item</param>
        public ConfigAttribute(string name, string description, ConfigFlag flags = ConfigFlag.None, string groupName = "default") : this(name, description)
        {
            Name = name;
            Description = description;
            Flags = flags;
            GroupName = groupName;
        }

        /// <summary>
        /// The name of the configuration item
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// The description of the configuration item
        /// </summary>
        public string Description { get; } = description;

        /// <summary>
        /// The flags for the configuration item
        /// </summary>
        public ConfigFlag Flags { get; set; }

        /// <summary>
        /// The group name for the configuration item
        /// </summary>
        public string GroupName { get; set; } = "default";
    }
}