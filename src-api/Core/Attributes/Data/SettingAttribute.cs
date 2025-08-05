namespace Kitsune.SDK.Core.Attributes.Data
{
    /// <summary>
    /// Attribute for marking setting properties
    /// </summary>
    /// <remarks>
    /// Creates a new setting attribute
    /// </remarks>
    /// <param name="name">The name of the setting item</param>
    /// <param name="description">The description of the setting item</param>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SettingAttribute(string name, string description) : Attribute
    {
        /// <summary>
        /// The name of the setting item
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// The description of the setting item
        /// </summary>
        public string Description { get; } = description;
    }
}