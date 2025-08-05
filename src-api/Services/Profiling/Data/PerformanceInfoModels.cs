
namespace Kitsune.SDK.Services.Profiling.Data
{
    /// <summary>
    /// Method performance information.
    /// </summary>
    public class MethodPerformanceInfo
    {
        /// <summary>
        /// Gets or sets the method name.
        /// </summary>
        public string? MethodName { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time in milliseconds.
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the total execution time in milliseconds across all calls.
        /// </summary>
        public double TotalExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the call count.
        /// </summary>
        public int CallCount { get; set; }

        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        public string? Category { get; set; }
    }

    /// <summary>
    /// Memory usage information.
    /// </summary>
    public class MemoryUsageInfo
    {
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        public string? OperationName { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory delta in bytes.
        /// </summary>
        public long MemoryDeltaBytes { get; set; }

        /// <summary>
        /// Gets or sets the total memory delta in bytes across all calls.
        /// </summary>
        public long TotalMemoryDeltaBytes { get; set; }

        /// <summary>
        /// Gets or sets the call count.
        /// </summary>
        public int CallCount { get; set; }

        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a potential memory leak was detected.
        /// </summary>
        public bool PotentialLeakDetected { get; set; }
    }
}