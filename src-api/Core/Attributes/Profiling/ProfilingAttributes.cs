
namespace Kitsune.SDK.Core.Attributes.Profiling
{
    /// <summary>
    /// Attribute to mark methods or classes that should be skipped during profiling.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SkipProfilingAttribute"/> class.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class SkipProfilingAttribute() : Attribute
    {
        // This is a marker interface only - no methods to implement
    }
}