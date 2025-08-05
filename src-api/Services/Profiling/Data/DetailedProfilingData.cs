using Microsoft.Extensions.Logging;
using Kitsune.SDK.Services.Profiling.Core;

namespace Kitsune.SDK.Services.Profiling.Data
{
    /// <summary>
    /// Detailed profiling data with support for steps and metadata.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of DetailedProfilingData.
    /// </remarks>
    public class DetailedProfilingData(string name, string pluginName, double elapsedMilliseconds, double warningThresholdMs, double criticalThresholdMs, string? description = null) : BaseProfilingData(name, pluginName, description, false, "", 0, "")
    {
        /// <summary>
        /// Gets or sets the elapsed time in milliseconds.
        /// </summary>
        public double ElapsedMilliseconds { get; set; } = elapsedMilliseconds;

        /// <summary>
        /// Gets or sets the warning threshold in milliseconds.
        /// </summary>
        public double WarningThresholdMs { get; set; } = warningThresholdMs;

        /// <summary>
        /// Gets or sets the critical threshold in milliseconds.
        /// </summary>
        public double CriticalThresholdMs { get; set; } = criticalThresholdMs;

        /// <summary>
        /// Gets or sets the memory before operation.
        /// </summary>
        public long MemoryBefore { get; set; }

        /// <summary>
        /// Gets or sets the memory after operation.
        /// </summary>
        public long MemoryAfter { get; set; }

        /// <summary>
        /// Gets or sets the memory delta.
        /// </summary>
        public long MemoryDelta { get; set; }

        /// <summary>
        /// Gets or sets the exception if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets custom metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = [];

        /// <summary>
        /// Gets or sets the steps within this profiling scope.
        /// </summary>
        public List<StepData> Steps { get; set; } = [];

        /// <summary>
        /// Gets the severity level based on thresholds.
        /// </summary>
        public override LogLevel Severity
        {
            get
            {
                if (Exception != null)
                    return LogLevel.Error;

                if (ElapsedMilliseconds >= CriticalThresholdMs)
                    return LogLevel.Error;

                if (ElapsedMilliseconds >= WarningThresholdMs)
                    return LogLevel.Warning;

                return LogLevel.Information;
            }
        }

        /// <summary>
        /// Represents a single step within the profiling scope.
        /// </summary>
        public class StepData
        {
            /// <summary>
            /// Gets or sets the step name.
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Gets or sets the duration in milliseconds.
            /// </summary>
            public double Duration { get; set; }

            /// <summary>
            /// Gets or sets the start time relative to scope start.
            /// </summary>
            public double StartTime { get; set; }

            /// <summary>
            /// Gets or sets the end time relative to scope start.
            /// </summary>
            public double EndTime { get; set; }

            /// <summary>
            /// Gets or sets the memory delta for this step.
            /// </summary>
            public long MemoryDelta { get; set; }

            /// <summary>
            /// Gets or sets the step description.
            /// </summary>
            public string? Description { get; set; }

            /// <summary>
            /// Gets or sets step-specific metadata.
            /// </summary>
            public Dictionary<string, object> Metadata { get; set; } = [];
        }
    }
}