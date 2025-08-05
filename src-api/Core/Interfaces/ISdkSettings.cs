using Kitsune.SDK.Core.Base;

namespace Kitsune.SDK.Core.Interfaces
{
    /// <summary>
    /// Marker interface for typed plugin settings
    /// This interface is used only for type registration and does not require
    /// any method implementations. It's used by the SDK to automatically register
    /// the settings type with the database system.
    /// </summary>
    /// <typeparam name="TSettings">The plugin's settings class type</typeparam>
    public interface ISdkSettings<TSettings> where TSettings : class, new()
    {
        // This is a marker interface only - no methods to implement
    }
}