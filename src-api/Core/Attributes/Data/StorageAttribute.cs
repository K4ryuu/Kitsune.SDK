namespace Kitsune.SDK.Core.Attributes.Data
{
    /// <summary>
    /// Attribute for marking storage properties
    /// </summary>
    /// <remarks>
    /// Creates a new storage attribute
    /// </remarks>
    /// <param name="name">The name of the storage item</param>
    /// <param name="description">The description of the storage item</param>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class StorageAttribute(string name, string description, bool track = false) : Attribute
    {
        /// <summary>
        /// The name of the storage item
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// The description of the storage item
        /// </summary>
        public string Description { get; } = description;

        /// <summary>
        /// When true, the value will be synchronized with the database before applying changes.
        /// This ensures external modifications (e.g., from web panels) are respected.
        /// Works with all data types.
        /// </summary>
        public bool Track { get; } = track;
    }
}