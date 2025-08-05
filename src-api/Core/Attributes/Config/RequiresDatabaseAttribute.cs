using System;

namespace Kitsune.SDK.Core.Attributes.Config
{
    /// <summary>
    /// Indicates that this configuration class requires database functionality.
    /// When applied to a config class, the SDK will automatically register database configurations
    /// and optionally validate the database connection during plugin initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class RequiresDatabaseAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether the database requirement is optional.
        /// If true, the plugin can run without a valid database configuration (database configs are created but not validated).
        /// If false, the plugin requires a valid database configuration and will be validated.
        /// Default is false (database is required).
        /// </summary>
        public bool Optional { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the plugin should fail to load if database validation fails.
        /// Only applies when Optional = false.
        /// Default is false (plugin loads but database features are disabled).
        /// </summary>
        public bool FailOnInvalidDatabase { get; set; } = false;

        /// <summary>
        /// Gets or sets a custom message to display when database validation fails.
        /// Only applies when Optional = false.
        /// </summary>
        public string? FailureMessage { get; set; }
    }
}