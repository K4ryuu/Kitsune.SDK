namespace Kitsune.SDK.Core.Attributes.Config
{
    /// <summary>
    /// Attribute for marking configuration validation methods
    /// </summary>
    /// <remarks>
    /// Creates a new configuration validation attribute
    /// </remarks>
    /// <param name="validationMethodName">The name of the validation method</param>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class ConfigValidationAttribute(string validationMethodName) : Attribute
    {
        /// <summary>
        /// The name of the validation method
        /// </summary>
        public string ValidationMethodName { get; } = validationMethodName;
    }
}