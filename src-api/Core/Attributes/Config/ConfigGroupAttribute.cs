namespace Kitsune.SDK.Core.Attributes.Config
{
    /// <summary>
    /// Attribute for marking configuration group properties
    /// </summary>
    /// <remarks>
    /// Creates a new configuration group attribute
    /// </remarks>
    /// <param name="name">The name of the configuration group (used as filename)</param>
    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigGroupAttribute(string name) : Attribute
    {
        /// <summary>
        /// The name of the configuration group
        /// </summary>
        public string Name { get; } = name;
    }
}