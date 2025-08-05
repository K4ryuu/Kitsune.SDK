using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Kitsune.SDK.Services.Profiling.Core
{
    /// <summary>
    /// Base class for profiling data.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BaseProfilingData"/> class.
    /// </remarks>
    /// <param name="name">The name of the object being profiled.</param>
    /// <param name="pluginName">The name of the plugin being profiled.</param>
    /// <param name="description">The description of the profiling data.</param>
    /// <param name="captureStackTrace">Whether to capture the stack trace.</param>
    /// <param name="sourceFile">The source file name where the profiling was initiated.</param>
    /// <param name="sourceLineNumber">The source line number where the profiling was initiated.</param>
    /// <param name="sourceMemberName">The source member name where the profiling was initiated.</param>
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract class BaseProfilingData(string name, string pluginName, string? description = null, bool captureStackTrace = false, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLineNumber = 0, [CallerMemberName] string sourceMemberName = "")
    {
        /// <summary>
        /// Gets the unique identifier for this profiling data.
        /// </summary>
        public string Id { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets the name of the object being profiled.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the description of the profiling data.
        /// </summary>
        public string? Description { get; set; } = description;

        /// <summary>
        /// Gets the timestamp when the profiling was started.
        /// </summary>
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the name of the plugin being profiled.
        /// </summary>
        public string PluginName { get; } = pluginName;

        /// <summary>
        /// Gets the source file name where the profiling was initiated.
        /// </summary>
        public string SourceFile { get; } = sourceFile;

        /// <summary>
        /// Gets the source line number where the profiling was initiated.
        /// </summary>
        public int SourceLineNumber { get; } = sourceLineNumber;

        /// <summary>
        /// Gets the source member name where the profiling was initiated.
        /// </summary>
        public string SourceMemberName { get; } = sourceMemberName;

        /// <summary>
        /// Gets the capture stack trace if available.
        /// </summary>
        public string? StackTrace { get; } = captureStackTrace ? Environment.StackTrace : null;

        /// <summary>
        /// Gets the severity level of this profiling data.
        /// </summary>
        public virtual LogLevel Severity => LogLevel.Information;

        /// <summary>
        /// Provides a string representation of the profiling data.
        /// </summary>
        /// <returns>A string representation of the profiling data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => $"{Name}: {GetType().Name} at {Timestamp:HH:mm:ss.fff}";
    }
}