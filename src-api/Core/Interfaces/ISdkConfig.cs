namespace Kitsune.SDK.Core.Interfaces
{
    /// <summary>
    /// Generic interface for typed plugin configuration
    /// </summary>
    /// <typeparam name="TConfig">The plugin's configuration class type</typeparam>
    public interface ISdkConfig<TConfig> where TConfig : class, new()
    {
        /// <summary>
        /// The SDK automatically provides the Config instance
        /// </summary>
        TConfig Config { get; }
    }
}