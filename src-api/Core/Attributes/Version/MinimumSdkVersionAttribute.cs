using System.Runtime.CompilerServices;

namespace Kitsune.SDK.Core.Attributes.Version
{
    /// <summary>
    /// Attribute to specify the minimum SDK version required for a plugin.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="MinimumSdkVersionAttribute"/> class.
    /// </remarks>
    /// <param name="version">The minimum SDK version required.</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MinimumSdkVersionAttribute(int version) : Attribute
    {
        /// <summary>
        /// The minimum SDK version required.
        /// </summary>
        public int Version { get; } = version;

        /// <summary>
        /// Gets a value indicating whether the specified SDK version is compatible
        /// </summary>
        /// <param name="currentVersion">The current SDK version</param>
        /// <returns>True if compatible, otherwise false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCompatible(int currentVersion) => currentVersion >= Version;
    }
}