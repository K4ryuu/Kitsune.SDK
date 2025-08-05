using System.Runtime.CompilerServices;
using System.Text;

namespace Kitsune.SDK.Services.Profiling.Data
{
    /// <summary>
    /// Profiling statistics for a plugin.
    /// </summary>
    public class ProfilingStatistics
    {
        /// <summary>
        /// Gets or sets the name of the plugin.
        /// </summary>
        public string? PluginName { get; set; }

        /// <summary>
        /// Gets or sets the profiling start time.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets the profiling end time.
        /// </summary>
        public DateTimeOffset EndTime { get; set; }

        /// <summary>
        /// Gets the total profiling duration.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Gets or sets the total number of profiling records.
        /// </summary>
        public int TotalProfilingRecords { get; set; }

        /// <summary>
        /// Gets or sets the number of method execution records.
        /// </summary>
        public int MethodExecutionCount { get; set; }

        /// <summary>
        /// Gets or sets the number of memory profiling records.
        /// </summary>
        public int MemoryProfilingCount { get; set; }


        /// <summary>
        /// Gets or sets the average method execution time in milliseconds.
        /// </summary>
        public double AverageMethodExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum method execution time in milliseconds.
        /// </summary>
        public double MaxMethodExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the total memory delta in bytes.
        /// </summary>
        public long TotalMemoryDeltaBytes { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory delta in bytes.
        /// </summary>
        public long MaxMemoryDeltaBytes { get; set; }


        /// <summary>
        /// Gets or sets the number of method execution warnings.
        /// </summary>
        public int MethodExecutionWarningCount { get; set; }

        /// <summary>
        /// Gets or sets the number of method execution errors.
        /// </summary>
        public int MethodExecutionErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of memory usage warnings.
        /// </summary>
        public int MemoryWarningCount { get; set; }

        /// <summary>
        /// Gets or sets the number of memory usage errors.
        /// </summary>
        public int MemoryErrorCount { get; set; }


        /// <summary>
        /// Gets the total number of warnings.
        /// </summary>
        public int TotalWarningCount => MethodExecutionWarningCount + MemoryWarningCount;

        /// <summary>
        /// Gets the total number of errors.
        /// </summary>
        public int TotalErrorCount => MethodExecutionErrorCount + MemoryErrorCount;

        /// <summary>
        /// Gets or sets the top slow methods.
        /// </summary>
        public List<MethodPerformanceInfo> TopSlowMethods { get; set; } = [];

        /// <summary>
        /// Gets or sets the top memory consumers.
        /// </summary>
        public List<MemoryUsageInfo> TopMemoryConsumers { get; set; } = [];


        /// <summary>
        /// Creates a new profiling statistics object for a plugin with default values.
        /// </summary>
        /// <param name="pluginName">The name of the plugin.</param>
        /// <param name="startTime">Optional start time.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProfilingStatistics CreateDefault(string pluginName, DateTimeOffset? startTime = null)
        {
            return new ProfilingStatistics
            {
                PluginName = pluginName,
                StartTime = startTime ?? DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow,
                TotalProfilingRecords = 0
            };
        }

        /// <summary>
        /// Generates a summary of the profiling statistics.
        /// </summary>
        /// <returns>A summary string of key statistics.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Profiling Summary for {PluginName}");
            sb.AppendLine($"Duration: {Duration.TotalMinutes:F2} minutes");
            sb.AppendLine($"Total Records: {TotalProfilingRecords}");
            sb.AppendLine($"Warnings: {TotalWarningCount}, Errors: {TotalErrorCount}");

            if (MethodExecutionCount > 0)
            {
                sb.AppendLine($"Methods: {MethodExecutionCount} executions, Avg: {AverageMethodExecutionTimeMs:F2}ms, Max: {MaxMethodExecutionTimeMs:F2}ms");
            }

            if (MemoryProfilingCount > 0)
            {
                double maxMemoryMB = MaxMemoryDeltaBytes / (1024.0 * 1024.0);
                sb.AppendLine($"Memory: {MemoryProfilingCount} ops, Total: {TotalMemoryDeltaBytes / (1024.0 * 1024.0):F2}MB, Max: {maxMemoryMB:F2}MB");
            }


            return sb.ToString();
        }
    }
}