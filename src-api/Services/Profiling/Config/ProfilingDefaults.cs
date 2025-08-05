
namespace Kitsune.SDK.Services.Profiling.Config
{
    /// <summary>
    /// Default values for profiling configuration used across the SDK
    /// </summary>
    public static class ProfilingDefaults
    {
        /// <summary>
        /// Default value for whether profiling is enabled
        /// </summary>
        public const bool Enabled = true;

        /// <summary>
        /// Default value for whether to include stack traces
        /// </summary>
        public const bool IncludeStackTraces = false;


        /// <summary>
        /// Default execution time threshold in milliseconds
        /// </summary>
        public const double ExecutionTimeThresholdMs = 20;

        /// <summary>
        /// Default memory usage threshold in bytes (1MB)
        /// </summary>
        public const long MemoryUsageThresholdBytes = 1024 * 1024;


        /// <summary>
        /// Default sampling rate for profiling (1.0 = 100%)
        /// </summary>
        public const double SamplingRate = 1.0;

        /// <summary>
        /// Default maximum profiling records to keep in memory
        /// </summary>
        public const int MaxProfilingRecords = 1000;

        /// <summary>
        /// Default maximum records before dumping to file
        /// </summary>
        public const int MaxRecordsBeforeDump = 500;

        /// <summary>
        /// Default minimum seconds between data dumps
        /// </summary>
        public const int MinSecondsBetweenDumps = 60;

        /// <summary>
        /// Default value for showing critical warnings
        /// </summary>
        public const bool ShowCriticalWarnings = true;
    }
}