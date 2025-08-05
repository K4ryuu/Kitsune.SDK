
namespace Kitsune.SDK.Core.Interfaces
{
    /// <summary>
    /// Marker interface for typed plugin storage
    /// This interface is used only for type registration and does not require
    /// any method implementations. It's used by the SDK to automatically register
    /// the storage type with the database system.
    /// </summary>
    /// <typeparam name="TStorage">The plugin's storage class type</typeparam>
    public interface ISdkStorage<TStorage> where TStorage : class, new()
    {
        // This is a marker interface only - no methods to implement
    }
}