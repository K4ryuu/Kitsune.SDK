using System.Runtime.CompilerServices;

namespace Kitsune.SDK.Services.Profiling.Core
{
    /// <summary>
    /// A no-operation IDisposable implementation for disabled profiling
    /// </summary>
    public sealed class NoOpDisposable : IDisposable
    {
        // Singleton instance for better memory usage
        public static readonly NoOpDisposable Instance = new();

        // Private constructor to enforce singleton pattern
        private NoOpDisposable() { }

        /// <summary>
        /// Does nothing - this is a no-op implementation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }
    }
}