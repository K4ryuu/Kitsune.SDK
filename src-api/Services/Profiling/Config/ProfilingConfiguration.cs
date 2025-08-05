using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Kitsune.SDK.Services.Profiling.Config
{
    /// <summary>
    /// Configuration for the SDK profiling system.
    /// </summary>
    public class ProfilingConfiguration
    {
        /// <summary>
        /// Gets or sets whether profiling is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the minimum log level for profiling events.
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; }

        /// <summary>
        /// Gets or sets whether to include stack traces in profiling reports.
        /// </summary>
        public bool IncludeStackTraces { get; set; }


        /// <summary>
        /// Gets or sets the threshold for method execution time in milliseconds.
        /// </summary>
        public double ExecutionTimeThresholdMs { get; set; }

        /// <summary>
        /// Gets or sets the threshold for memory usage in bytes.
        /// </summary>
        public long MemoryUsageThresholdBytes { get; set; }


        /// <summary>
        /// Gets or sets the sampling rate for profiling (between 0.0 and 1.0).
        /// </summary>
        public double SamplingRate { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of profiling records to keep in memory.
        /// </summary>
        public int MaxProfilingRecords { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of records before data is dumped to a temporary file.
        /// </summary>
        public int MaxRecordsBeforeDump { get; set; }

        /// <summary>
        /// Gets or sets the minimum seconds between temporary data dumps.
        /// </summary>
        public int MinSecondsBetweenDumps { get; set; }

        /// <summary>
        /// Gets or sets whether to show critical profiling warnings in the console.
        /// This will always be true if profiling is enabled.
        /// </summary>
        public bool ShowCriticalWarnings { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilingConfiguration"/> class with default values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProfilingConfiguration()
        {
            // Use ProfilingDefaults for initialization
            Enabled = ProfilingDefaults.Enabled;
            MinimumLogLevel = LogLevel.Information;
            IncludeStackTraces = ProfilingDefaults.IncludeStackTraces;
            ExecutionTimeThresholdMs = ProfilingDefaults.ExecutionTimeThresholdMs;
            MemoryUsageThresholdBytes = ProfilingDefaults.MemoryUsageThresholdBytes;
            SamplingRate = ProfilingDefaults.SamplingRate;
            MaxProfilingRecords = ProfilingDefaults.MaxProfilingRecords;
            MaxRecordsBeforeDump = ProfilingDefaults.MaxRecordsBeforeDump;
            MinSecondsBetweenDumps = ProfilingDefaults.MinSecondsBetweenDumps;
            ShowCriticalWarnings = ProfilingDefaults.ShowCriticalWarnings;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfilingConfiguration"/> class with custom values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProfilingConfiguration(bool enabled, LogLevel minimumLogLevel, double executionTimeThresholdMs, long memoryUsageThresholdBytes)
        {
            // Initialize with specified parameters
            Enabled = enabled;
            MinimumLogLevel = minimumLogLevel;
            ExecutionTimeThresholdMs = executionTimeThresholdMs;
            MemoryUsageThresholdBytes = memoryUsageThresholdBytes;

            // Use defaults for other values
            IncludeStackTraces = ProfilingDefaults.IncludeStackTraces;
            SamplingRate = ProfilingDefaults.SamplingRate;
            MaxProfilingRecords = ProfilingDefaults.MaxProfilingRecords;
            MaxRecordsBeforeDump = ProfilingDefaults.MaxRecordsBeforeDump;
            MinSecondsBetweenDumps = ProfilingDefaults.MinSecondsBetweenDumps;
            ShowCriticalWarnings = ProfilingDefaults.ShowCriticalWarnings;
        }
    }
}