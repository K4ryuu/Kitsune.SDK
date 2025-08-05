using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Services.Profiling.Core;

namespace Kitsune.SDK.Services.Profiling.Data
{
    /// <summary>
    /// Method execution profiling data.
    /// </summary>
    public class MethodExecutionProfilingData : BaseProfilingData
    {
        /// <summary>
        /// Gets the elapsed time in milliseconds.
        /// </summary>
        public double ElapsedMilliseconds { get; }

        /// <summary>
        /// Gets the warning threshold in milliseconds.
        /// </summary>
        public double WarningThresholdMs { get; }

        /// <summary>
        /// Gets the critical threshold in milliseconds.
        /// </summary>
        public double CriticalThresholdMs { get; }

        /// <summary>
        /// Gets information about method arguments if available.
        /// </summary>
        public Dictionary<string, object>? Arguments { get; }

        /// <summary>
        /// Gets information about the return value if available.
        /// </summary>
        public object? ReturnValue { get; }

        /// <summary>
        /// Gets a value indicating whether the method threw an exception.
        /// </summary>
        public bool HasException { get; }

        /// <summary>
        /// Gets the exception thrown by the method if any.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the severity level of this profiling data.
        /// </summary>
        public override LogLevel Severity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodExecutionProfilingData"/> class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodExecutionProfilingData(string name, string pluginName, double elapsedMilliseconds, double warningThresholdMs, double criticalThresholdMs, Dictionary<string, object>? arguments = null, object? returnValue = null, Exception? exception = null, string? description = null, bool captureStackTrace = false, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLineNumber = 0, [CallerMemberName] string sourceMemberName = "") : base(name, pluginName, description, captureStackTrace, sourceFile, sourceLineNumber, sourceMemberName)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            WarningThresholdMs = warningThresholdMs;
            CriticalThresholdMs = criticalThresholdMs;
            Arguments = arguments;
            ReturnValue = returnValue;
            Exception = exception;
            HasException = exception != null;

            // Determine severity based on thresholds
            Severity = ElapsedMilliseconds >= CriticalThresholdMs ? LogLevel.Error : ElapsedMilliseconds >= WarningThresholdMs ? LogLevel.Warning : LogLevel.Information;
        }
    }

    /// <summary>
    /// Memory usage profiling data.
    /// </summary>
    public class MemoryProfilingData : BaseProfilingData
    {
        /// <summary>
        /// Gets the memory usage before the operation in bytes.
        /// </summary>
        public long MemoryBefore { get; }

        /// <summary>
        /// Gets the memory usage after the operation in bytes.
        /// </summary>
        public long MemoryAfter { get; }

        /// <summary>
        /// Gets the memory delta in bytes.
        /// </summary>
        public long MemoryDelta { get; }

        /// <summary>
        /// Gets the warning threshold in bytes.
        /// </summary>
        public long WarningThresholdBytes { get; }

        /// <summary>
        /// Gets the critical threshold in bytes.
        /// </summary>
        public long CriticalThresholdBytes { get; }

        /// <summary>
        /// Gets a value indicating whether detailed memory profiling is enabled.
        /// </summary>
        public bool DetailedProfilingEnabled { get; }

        /// <summary>
        /// Gets the detailed allocation information if available.
        /// </summary>
        public Dictionary<string, long>? DetailedAllocations { get; }

        /// <summary>
        /// Gets a value indicating whether a potential memory leak was detected.
        /// </summary>
        public bool PotentialLeakDetected { get; }

        /// <summary>
        /// Gets the severity level of this profiling data.
        /// </summary>
        public override LogLevel Severity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryProfilingData"/> class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryProfilingData(string name, string pluginName, long memoryBefore, long memoryAfter, long warningThresholdBytes, long criticalThresholdBytes, bool detailedProfilingEnabled = false, Dictionary<string, long>? detailedAllocations = null, bool potentialLeakDetected = false, string? description = null, bool captureStackTrace = false, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLineNumber = 0, [CallerMemberName] string sourceMemberName = "") : base(name, pluginName, description, captureStackTrace, sourceFile, sourceLineNumber, sourceMemberName)
        {
            MemoryBefore = memoryBefore;
            MemoryAfter = memoryAfter;
            MemoryDelta = memoryAfter - memoryBefore;
            WarningThresholdBytes = warningThresholdBytes;
            CriticalThresholdBytes = criticalThresholdBytes;
            DetailedProfilingEnabled = detailedProfilingEnabled;
            DetailedAllocations = detailedAllocations;
            PotentialLeakDetected = potentialLeakDetected;

            // Determine severity based on thresholds
            Severity = (MemoryDelta >= CriticalThresholdBytes || PotentialLeakDetected) ? LogLevel.Error : MemoryDelta >= WarningThresholdBytes ? LogLevel.Warning : LogLevel.Information;
        }
    }

}