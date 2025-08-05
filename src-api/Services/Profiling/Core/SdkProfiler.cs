using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Kitsune.SDK.Services.Profiling.Config;
using Kitsune.SDK.Services.Profiling.Data;
using Kitsune.SDK.Services.Profiling.Profilers;
using Microsoft.Extensions.Logging;

namespace Kitsune.SDK.Services.Profiling.Core
{
    /// <summary>
    /// The main profiler for the SDK.
    /// </summary>
    public class SdkProfiler : IDisposable
    {
        // Constants for performance tuning - easier to adjust in one place
        private const int FILE_WRITE_BUFFER_SIZE = 8192;
        private const int TEMP_REPORT_FILE_LIMIT = 25;

        // Static shared resources
        private static readonly Lazy<SdkProfiler> _instance = new(() => new SdkProfiler());

        // Concurrency-safe collections
        private readonly ConcurrentDictionary<string, ConcurrentQueue<BaseProfilingData>> _profilingData;
        private readonly object _lockObject = new();
        private readonly ILogger _logger;
        private bool _isDisposed;
        private readonly ConcurrentDictionary<string, string> _pluginDirectories = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastDumpTimes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the singleton instance of the SdkProfiler.
        /// </summary>
        public static SdkProfiler Instance => _instance.Value;

        /// <summary>
        /// Gets the configuration for the profiler.
        /// </summary>
        public ProfilingConfiguration Configuration { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the profiler is enabled.
        /// </summary>
        public bool IsEnabled => Configuration?.Enabled ?? false;

        /// <summary>
        /// Gets the number of profiling records collected.
        /// </summary>
        public int ProfiledCount => _profilingData.Values.Sum(queue => queue.Count);

        /// <summary>
        /// Gets the profiling start time.
        /// </summary>
        public DateTimeOffset ProfilingStartTime { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SdkProfiler"/> class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SdkProfiler()
        {
            _profilingData = new ConcurrentDictionary<string, ConcurrentQueue<BaseProfilingData>>(StringComparer.OrdinalIgnoreCase);
            ProfilingStartTime = DateTimeOffset.UtcNow;

            // Default configuration
            Configuration = new ProfilingConfiguration();

            // Initialize logger with console output
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger("SdkProfiler");
        }

        /// <summary>
        /// Gets a plugin-specific logger instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILogger GetPluginLogger(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName))
                return _logger;

            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            return loggerFactory.CreateLogger($"SdkProfiler-{pluginName}");
        }

        /// <summary>
        /// Registers a plugin's directory for storing profiling data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterPluginDirectory(string pluginName, string pluginDirectory)
        {
            if (string.IsNullOrEmpty(pluginName) || string.IsNullOrEmpty(pluginDirectory))
                return;

            _pluginDirectories[pluginName] = pluginDirectory;
        }

        /// <summary>
        /// Gets the profiling directory for a plugin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetPluginProfilingDirectory(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName) || !_pluginDirectories.TryGetValue(pluginName, out string? pluginDirectory))
                return string.Empty;

            return Path.Combine(pluginDirectory, "profiling");
        }

        /// <summary>
        /// Configures the profiler.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Configure(ProfilingConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Creates an execution profiler to profile a method's execution time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable ProfileExecution(string methodName, string pluginName, double warningThresholdMs = 0, double criticalThresholdMs = 0, string? description = null, bool captureStackTrace = false, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLineNumber = 0, [CallerMemberName] string sourceMemberName = "")
        {
            if (!IsEnabled)
                return NoOpDisposable.Instance;

            // Use configuration thresholds if not specified
            if (warningThresholdMs <= 0)
                warningThresholdMs = Configuration.ExecutionTimeThresholdMs;

            if (criticalThresholdMs <= 0)
                criticalThresholdMs = warningThresholdMs * 2;

            return new ExecutionProfiler(this, methodName, pluginName, warningThresholdMs, criticalThresholdMs, description, captureStackTrace, sourceFile, sourceLineNumber, sourceMemberName);
        }

        /// <summary>
        /// Creates a memory profiler to profile memory usage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable ProfileMemory(string operationName, string pluginName, long warningThresholdBytes = 0, long criticalThresholdBytes = 0, bool detailedProfilingEnabled = false, string? description = null, bool captureStackTrace = false, [CallerFilePath] string sourceFile = "", [CallerLineNumber] int sourceLineNumber = 0, [CallerMemberName] string sourceMemberName = "")
        {
            if (!IsEnabled)
                return NoOpDisposable.Instance;

            // Use configuration thresholds if not specified
            if (warningThresholdBytes <= 0)
                warningThresholdBytes = Configuration.MemoryUsageThresholdBytes;

            if (criticalThresholdBytes <= 0)
                criticalThresholdBytes = warningThresholdBytes * 2;

            return new MemoryProfiler(this, operationName, pluginName, warningThresholdBytes, criticalThresholdBytes, detailedProfilingEnabled, description, captureStackTrace, sourceFile, sourceLineNumber, sourceMemberName);
        }

        /// <summary>
        /// Ensures the profiling directory exists for a plugin (lazy creation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureProfilingDirectoryExists(string pluginName)
        {
            string profilingDir = GetPluginProfilingDirectory(pluginName);
            if (!string.IsNullOrEmpty(profilingDir) && !Directory.Exists(profilingDir))
            {
                try
                {
                    Directory.CreateDirectory(profilingDir);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create profiling directory for {pluginName}: {profilingDir}");
                }
            }
        }

        /// <summary>
        /// Records profiling data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordProfilingData(BaseProfilingData data)
        {
            if (!IsEnabled)
                return;

            // Random sampling based on configuration
            if (Configuration.SamplingRate < 1.0 && Random.Shared.NextDouble() > Configuration.SamplingRate)
                return;

            string pluginName = data.PluginName;
            
            // Create profiling directory only when first profiling data is recorded
            EnsureProfilingDirectoryExists(pluginName);
            
            var dataQueue = _profilingData.GetOrAdd(pluginName, _ => new ConcurrentQueue<BaseProfilingData>());
            dataQueue.Enqueue(data);

            // Get logger instance for plugin
            var logger = GetPluginLogger(pluginName);

            if (data.Severity >= Configuration.MinimumLogLevel)
            {
                LogProfilingAlert(logger, data);
            }

            // Check if we need to dump data to a temporary file
            CheckAndDumpTemporaryData(pluginName);
        }

        /// <summary>
        /// Logs profiling alerts directly to the console.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogProfilingAlert(ILogger logger, BaseProfilingData data)
        {
            // Different log messages based on profiling data type
            switch (data)
            {
                case MethodExecutionProfilingData methodData:
                    string methodPrefix = data.Severity >= LogLevel.Warning ? "PERFORMANCE WARNING" : "PERFORMANCE";
                    string methodMessage = $"{methodPrefix}: Method {methodData.Name} took {methodData.ElapsedMilliseconds:F2}ms";
                    LogWithAppropriateLevel(logger, data.Severity, methodMessage);
                    break;

                case MemoryProfilingData memoryData:
                    string memoryPrefix = data.Severity >= LogLevel.Warning ? "MEMORY WARNING" : "MEMORY";
                    string memoryMessage = $"{memoryPrefix}: Operation {memoryData.Name} used {memoryData.MemoryDelta / 1024.0:F2}KB";
                    LogWithAppropriateLevel(logger, data.Severity, memoryMessage);
                    break;

            }
        }

        /// <summary>
        /// Logs a message with appropriate severity level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LogWithAppropriateLevel(ILogger logger, LogLevel severity, string message)
        {
            switch (severity)
            {
                case LogLevel.Error:
                    logger.LogError(message);
                    break;
                case LogLevel.Warning:
                    logger.LogWarning(message);
                    break;
                default:
                    logger.LogInformation(message);
                    break;
            }
        }




        /// <summary>
        /// Checks if temporary data needs to be dumped to a file to prevent memory consumption.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckAndDumpTemporaryData(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName) || !_profilingData.TryGetValue(pluginName, out var dataQueue))
                return;

            // Check count threshold
            if (dataQueue.Count < Configuration.MaxRecordsBeforeDump)
                return;

            // Check time threshold
            if (!_lastDumpTimes.TryGetValue(pluginName, out var lastDumpTime) || (DateTimeOffset.UtcNow - lastDumpTime).TotalSeconds < Configuration.MinSecondsBetweenDumps)
                return;

            // Dump to file - use lock to prevent multiple threads from dumping at the same time
            if (Monitor.TryEnter(_lockObject, 0))
            {
                try
                {
                    DumpTemporaryData(pluginName);
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            }
        }

        /// <summary>
        /// Dumps profiling data to a temporary file for later analysis.
        /// </summary>
        private void DumpTemporaryData(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName) || !_profilingData.TryGetValue(pluginName, out var dataQueue))
                return;

            try
            {
                // Get the profiling directory
                string profilingDir = GetPluginProfilingDirectory(pluginName);
                if (string.IsNullOrEmpty(profilingDir))
                {
                    _logger.LogError($"Cannot dump temporary data for {pluginName}: No profiling directory registered");
                    return;
                }

                // Create a new data queue to swap with the existing one
                var newDataQueue = new ConcurrentQueue<BaseProfilingData>();

                // Determine output file path
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"temp_{pluginName.Replace(" ", "_")}_{timestamp}.md";
                string filePath = Path.Combine(profilingDir, fileName);

                // Move all data to a local list for processing
                List<BaseProfilingData> dataToProcess = new(dataQueue.Count);
                while (dataQueue.TryDequeue(out var data))
                {
                    dataToProcess.Add(data);
                }

                // Clean up old temp files if there are too many
                CleanupOldTempFiles(profilingDir, pluginName);

                // Replace the data queue with the new empty one
                _profilingData[pluginName] = newDataQueue;

                // Update last dump time
                _lastDumpTimes[pluginName] = DateTimeOffset.UtcNow;

                // Write data to file
                if (dataToProcess.Count > 0)
                {
                    WriteTempDataToFile(filePath, pluginName, dataToProcess);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error dumping temporary data for {pluginName}");
            }
        }

        /// <summary>
        /// Cleans up old temporary files if there are too many.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CleanupOldTempFiles(string profilingDir, string pluginName)
        {
            try
            {
                // Get all temp files for this plugin
                string tempFilePattern = $"temp_{pluginName.Replace(" ", "_")}*.md";
                var tempFiles = Directory.GetFiles(profilingDir, tempFilePattern).OrderBy(File.GetCreationTime).ToArray();

                // If there are too many, delete the oldest ones
                if (tempFiles.Length > TEMP_REPORT_FILE_LIMIT)
                {
                    int filesToDelete = tempFiles.Length - TEMP_REPORT_FILE_LIMIT;
                    for (int i = 0; i < filesToDelete; i++)
                    {
                        try { File.Delete(tempFiles[i]); }
                        catch { /* Ignore deletion errors */ }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Writes profiling data to a temporary file.
        /// </summary>
        private void WriteTempDataToFile(string filePath, string pluginName, List<BaseProfilingData> dataToProcess)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, FILE_WRITE_BUFFER_SIZE);
            using var writer = new StreamWriter(fileStream);

            writer.WriteLine($"# Temporary profiling data for {pluginName}");
            writer.WriteLine($"Generated at: {DateTime.Now}");
            writer.WriteLine($"Records: {dataToProcess.Count}");
            writer.WriteLine();

            // Group data by type for easier analysis
            var methodData = dataToProcess.OfType<MethodExecutionProfilingData>().ToList();
            var memoryData = dataToProcess.OfType<MemoryProfilingData>().ToList();

            // Write method execution data
            if (methodData.Count > 0)
            {
                WriteTempMethodData(writer, methodData);
            }

            // Write memory usage data
            if (memoryData.Count > 0)
            {
                WriteTempMemoryData(writer, memoryData);
            }

        }

        /// <summary>
        /// Writes method execution data to the temporary file.
        /// </summary>
        private static void WriteTempMethodData(StreamWriter writer, List<MethodExecutionProfilingData> methodData)
        {
            writer.WriteLine($"## Method Execution Data: {methodData.Count} records");

            int count = 0;
            foreach (var data in methodData.OrderByDescending(d => d.ElapsedMilliseconds).Take(20))
            {
                count++;
                string performanceIndicator = data.ElapsedMilliseconds >= data.CriticalThresholdMs ? "⚠️ " :
                                              data.ElapsedMilliseconds >= data.WarningThresholdMs ? "⚡ " : "";

                writer.WriteLine($"### {count}. {performanceIndicator}{data.Name} - {data.ElapsedMilliseconds:F2}ms");
                writer.WriteLine($"- Thresholds: Warning={data.WarningThresholdMs}ms, Critical={data.CriticalThresholdMs}ms");
                writer.WriteLine($"- File: {data.SourceFile}:{data.SourceLineNumber}");

                if (data.HasException)
                {
                    writer.WriteLine($"- **Exception**: {data.Exception?.GetType().Name}: {data.Exception?.Message}");
                }

                writer.WriteLine();
            }

            writer.WriteLine();
        }

        /// <summary>
        /// Writes memory usage data to the temporary file.
        /// </summary>
        private static void WriteTempMemoryData(StreamWriter writer, List<MemoryProfilingData> memoryData)
        {
            writer.WriteLine($"## Memory Usage Data: {memoryData.Count} records");

            int count = 0;
            foreach (var data in memoryData.OrderByDescending(d => d.MemoryDelta).Take(20))
            {
                count++;
                string memoryIndicator = data.MemoryDelta >= data.CriticalThresholdBytes ? "🔥 " :
                                        data.MemoryDelta >= data.WarningThresholdBytes ? "⚠️ " : "";

                writer.WriteLine($"### {count}. {memoryIndicator}{data.Name} - {data.MemoryDelta / 1024:F2}KB");
                writer.WriteLine($"- Before: {data.MemoryBefore / 1024:F2}KB, After: {data.MemoryAfter / 1024:F2}KB");
                writer.WriteLine($"- Thresholds: Warning={data.WarningThresholdBytes / 1024:F2}KB, Critical={data.CriticalThresholdBytes / 1024:F2}KB");
                writer.WriteLine($"- File: {data.SourceFile}:{data.SourceLineNumber}");

                if (data.PotentialLeakDetected)
                {
                    writer.WriteLine($"- **⚠️ POTENTIAL MEMORY LEAK DETECTED!**");
                }

                writer.WriteLine();
            }

            writer.WriteLine();
        }


        /// <summary>
        /// Truncates a string and adds ellipsis if it's too long.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string TruncateWithEllipsis(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return string.Concat(text.AsSpan(0, maxLength - 3), "...");
        }

        /// <summary>
        /// Gets profiling data for a plugin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<BaseProfilingData> GetProfilingData(string pluginName)
        {
            if (!IsEnabled || string.IsNullOrEmpty(pluginName) || !_profilingData.TryGetValue(pluginName, out var dataQueue))
                return [];

            return [.. dataQueue];
        }

        /// <summary>
        /// Gets all profiling data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Dictionary<string, IEnumerable<BaseProfilingData>> GetAllProfilingData()
        {
            if (!IsEnabled)
                return [];

            var result = new Dictionary<string, IEnumerable<BaseProfilingData>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _profilingData)
            {
                result[pair.Key] = [.. pair.Value];
            }

            return result;
        }

        /// <summary>
        /// Gets profiling statistics for a plugin.
        /// </summary>
        public ProfilingStatistics GetProfilingStatistics(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName) || !_profilingData.TryGetValue(pluginName, out var dataQueue))
            {
                return ProfilingStatistics.CreateDefault(pluginName, ProfilingStartTime);
            }

            var stats = new ProfilingStatistics
            {
                PluginName = pluginName,
                StartTime = ProfilingStartTime,
                EndTime = DateTimeOffset.UtcNow,
                TotalProfilingRecords = dataQueue.Count
            };

            // Process the data to collect statistics
            var dataList = dataQueue.ToList();
            ProcessProfilingStatistics(stats, dataList);

            return stats;
        }

        /// <summary>
        /// Processes profiling data to gather statistics.
        /// </summary>
        private void ProcessProfilingStatistics(ProfilingStatistics stats, List<BaseProfilingData> dataList)
        {
            // Process method execution data
            var methodData = dataList.OfType<MethodExecutionProfilingData>().ToList();
            stats.MethodExecutionCount = methodData.Count;
            if (methodData.Count > 0)
            {
                ProcessMethodExecutionStatistics(stats, methodData);
            }

            // Process memory usage data
            var memoryData = dataList.OfType<MemoryProfilingData>().ToList();
            stats.MemoryProfilingCount = memoryData.Count;
            if (memoryData.Count > 0)
            {
                ProcessMemoryUsageStatistics(stats, memoryData);
            }

        }

        /// <summary>
        /// Processes method execution data for statistics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProcessMethodExecutionStatistics(ProfilingStatistics stats, List<MethodExecutionProfilingData> methodData)
        {
            stats.AverageMethodExecutionTimeMs = methodData.Average(d => d.ElapsedMilliseconds);
            stats.MaxMethodExecutionTimeMs = methodData.Max(d => d.ElapsedMilliseconds);
            stats.MethodExecutionWarningCount = methodData.Count(d => d.Severity == LogLevel.Warning);
            stats.MethodExecutionErrorCount = methodData.Count(d => d.Severity == LogLevel.Error);

            // Group method execution data by name and create performance info
            var methodInfos = methodData
                .GroupBy(d => d.Name)
                .Select(g => new MethodPerformanceInfo
                {
                    MethodName = g.Key,
                    ExecutionTimeMs = g.Max(d => d.ElapsedMilliseconds),
                    TotalExecutionTimeMs = g.Sum(d => d.ElapsedMilliseconds),
                    CallCount = g.Count(),
                    Category = "Method"
                })
                .ToList();

            // Add top slow methods
            stats.TopSlowMethods = [.. methodInfos.OrderByDescending(m => m.ExecutionTimeMs).Take(10)];
        }

        /// <summary>
        /// Processes memory usage data for statistics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProcessMemoryUsageStatistics(ProfilingStatistics stats, List<MemoryProfilingData> memoryData)
        {
            stats.TotalMemoryDeltaBytes = memoryData.Sum(d => d.MemoryDelta);
            stats.MaxMemoryDeltaBytes = memoryData.Max(d => d.MemoryDelta);
            stats.MemoryWarningCount = memoryData.Count(d => d.Severity == LogLevel.Warning);
            stats.MemoryErrorCount = memoryData.Count(d => d.Severity == LogLevel.Error);

            // Group memory usage data by name and create usage info
            var memoryInfos = memoryData
                .GroupBy(d => d.Name)
                .Select(g => new MemoryUsageInfo
                {
                    OperationName = g.Key,
                    MemoryDeltaBytes = g.Max(d => d.MemoryDelta),
                    TotalMemoryDeltaBytes = g.Sum(d => d.MemoryDelta),
                    CallCount = g.Count(),
                    Category = "Memory",
                    PotentialLeakDetected = g.Any(d => d.PotentialLeakDetected)
                })
                .ToList();

            // Add top memory consumers
            stats.TopMemoryConsumers = [.. memoryInfos.OrderByDescending(m => m.MemoryDeltaBytes).Take(10)];
        }


        /// <summary>
        /// Gets profiling statistics for all plugins.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<ProfilingStatistics> GetAllProfilingStatistics()
        {
            var result = new List<ProfilingStatistics>();
            foreach (var pluginName in _profilingData.Keys)
            {
                result.Add(GetProfilingStatistics(pluginName));
            }

            return result;
        }

        /// <summary>
        /// Clears all profiling data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearProfilingData(string? pluginName = null)
        {
            if (string.IsNullOrEmpty(pluginName))
            {
                // Clear all data
                _profilingData.Clear();
            }
            else
            {
                // Clear data for a specific plugin
                if (_profilingData.TryGetValue(pluginName, out _))
                {
                    _profilingData[pluginName] = new ConcurrentQueue<BaseProfilingData>();
                }
            }

        }

        /// <summary>
        /// Generates a profiling report for a plugin.
        /// </summary>
        public string GenerateProfilingReport(string pluginName)
        {
            var stats = GetProfilingStatistics(pluginName);
            var report = new StringBuilder(FILE_WRITE_BUFFER_SIZE);  // Preset capacity to avoid reallocations

            // Report header
            report.AppendLine($"===== PROFILING REPORT FOR {stats.PluginName} =====");
            report.AppendLine($"Generated at: {DateTime.Now}");
            report.AppendLine($"Time Range: {stats.StartTime} to {stats.EndTime} ({stats.Duration.TotalMinutes:F2} minutes)");
            report.AppendLine($"Total Records: {stats.TotalProfilingRecords}");
            report.AppendLine();

            // Method execution statistics
            AppendMethodExecutionStats(report, stats);

            // Memory usage statistics
            AppendMemoryUsageStats(report, stats);


            // End of report summary
            report.AppendLine($"===== END OF REPORT (Generated on {DateTime.Now}) =====");
            return report.ToString();
        }

        /// <summary>
        /// Appends method execution statistics to the report.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendMethodExecutionStats(StringBuilder report, ProfilingStatistics stats)
        {
            report.AppendLine("--- METHOD EXECUTION STATS ---");
            report.AppendLine($"Total Method Executions:    {stats.MethodExecutionCount}");
            report.AppendLine($"Average Execution Time:     {stats.AverageMethodExecutionTimeMs:F2}ms");
            report.AppendLine($"Max Execution Time:         {stats.MaxMethodExecutionTimeMs:F2}ms");
            report.AppendLine($"Warning Count:              {stats.MethodExecutionWarningCount}");
            report.AppendLine($"Error Count:                {stats.MethodExecutionErrorCount}");
            report.AppendLine();

            // Top slow methods with detailed information
            if (stats.TopSlowMethods.Count > 0)
            {
                report.AppendLine("TOP SLOW METHODS");
                int count = 0;
                foreach (var method in stats.TopSlowMethods)
                {
                    count++;
                    string methodName = method.MethodName ?? "Unknown";

                    // Add visual indicator for slow methods
                    string indicator = method.ExecutionTimeMs > 100 ? "! " :
                                     method.ExecutionTimeMs > 50 ? "* " : "";

                    report.AppendLine($"{count}. {indicator}{methodName}");
                    report.AppendLine($"   Max Time: {method.ExecutionTimeMs:F2}ms   Calls: {method.CallCount}");
                    report.AppendLine($"   Total Time: {method.TotalExecutionTimeMs:F2}ms");
                    report.AppendLine($"   Avg Per Call: {(method.TotalExecutionTimeMs / method.CallCount):F2}ms");
                    if (count < stats.TopSlowMethods.Count)
                        report.AppendLine();
                }
                report.AppendLine();
            }
            else
            {
                report.AppendLine("No method execution data available");
                report.AppendLine();
            }
        }

        /// <summary>
        /// Appends memory usage statistics to the report.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendMemoryUsageStats(StringBuilder report, ProfilingStatistics stats)
        {
            report.AppendLine("--- MEMORY USAGE STATS ---");
            report.AppendLine($"Total Memory Operations:    {stats.MemoryProfilingCount}");
            double totalMemoryMB = stats.TotalMemoryDeltaBytes / (1024.0 * 1024.0);
            double maxMemoryMB = stats.MaxMemoryDeltaBytes / (1024.0 * 1024.0);
            report.AppendLine($"Total Memory Delta:       {totalMemoryMB:F2} MB");
            report.AppendLine($"Max Memory Delta:         {maxMemoryMB:F2} MB");
            double avgMemoryMB = stats.MemoryProfilingCount > 0 ? totalMemoryMB / stats.MemoryProfilingCount : 0;
            report.AppendLine($"Avg Memory Delta:         {avgMemoryMB:F2} MB");
            report.AppendLine($"Warning Count:            {stats.MemoryWarningCount}");
            report.AppendLine($"Error Count:              {stats.MemoryErrorCount}");
            report.AppendLine();

            // Top memory consumers with detailed information
            if (stats.TopMemoryConsumers.Count > 0)
            {
                report.AppendLine("TOP MEMORY CONSUMERS");
                int count = 0;
                foreach (var op in stats.TopMemoryConsumers)
                {
                    count++;
                    string operationName = op.OperationName ?? "Unknown";
                    double memoryMB = op.MemoryDeltaBytes / (1024.0 * 1024.0);

                    // Add visual indicator for high memory usage
                    string indicator = memoryMB > 10 ? "! " :
                                     memoryMB > 5 ? "* " : "";

                    report.AppendLine($"{count}. {indicator}{operationName}");
                    report.AppendLine($"   Max Memory: {memoryMB:F2} MB   Calls: {op.CallCount}");
                    double opTotalMemoryMB = op.TotalMemoryDeltaBytes / (1024.0 * 1024.0);
                    report.AppendLine($"   Total Memory: {opTotalMemoryMB:F2} MB");
                    report.AppendLine($"   Avg Per Call: {(opTotalMemoryMB / op.CallCount):F2} MB");
                    if (count < stats.TopMemoryConsumers.Count)
                        report.AppendLine();
                }
                report.AppendLine();
            }
            else
            {
                report.AppendLine("No memory usage data available");
                report.AppendLine();
            }
        }


        /// <summary>
        /// Saves a complete profiling report for a specific plugin, including any data from temporary files.
        /// </summary>
        public string? SaveFinalProfilingReport(string pluginName, bool isShutdownReport = false)
        {
            if (!IsEnabled || string.IsNullOrEmpty(pluginName))
                return null;

            try
            {
                // Get the profiling directory
                string profilingDir = GetPluginProfilingDirectory(pluginName);
                if (string.IsNullOrEmpty(profilingDir))
                {
                    _logger.LogError($"Cannot save profiling report for {pluginName}: No profiling directory registered");
                    return null;
                }

                // Generate current memory data report
                string report = GenerateProfilingReport(pluginName);

                // Find temporary data files for this plugin
                string tempFilePattern = $"temp_{pluginName.Replace(" ", "_")}*.md";
                string[] tempFiles = Directory.GetFiles(profilingDir, tempFilePattern);

                // Determine output file path
                string reportType = isShutdownReport ? "shutdown" : "profile";
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string extension = ".md";
                string fileName = $"{pluginName.Replace(" ", "_")}_{reportType}_{timestamp}{extension}";
                string filePath = Path.Combine(profilingDir, fileName);

                // Generate the header
                var plugin = _profilingData.TryGetValue(pluginName, out _) ? pluginName : "Unknown";
                string headerLine = $"# {(isShutdownReport ? "SHUTDOWN " : "")}PROFILING REPORT FOR {plugin}\n" +
                                    $"Generated at: {DateTime.Now}\n\n";

                // Write report to file
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, FILE_WRITE_BUFFER_SIZE))
                using (var writer = new StreamWriter(fileStream))
                {
                    writer.WriteLine(headerLine);

                    // Write current memory data
                    writer.WriteLine("## CURRENT MEMORY DATA");
                    writer.WriteLine(report);
                    writer.WriteLine();

                    // Add data from temporary files if any exist
                    if (tempFiles.Length > 0)
                    {
                        writer.WriteLine("## ARCHIVED DATA FROM TEMPORARY FILES");

                        // Sort temp files by creation time
                        Array.Sort(tempFiles, (a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

                        foreach (string tempFile in tempFiles)
                        {
                            writer.WriteLine($"### Data from {Path.GetFileName(tempFile)}");

                            try
                            {
                                // Read and append the temporary file content
                                string tempContent = File.ReadAllText(tempFile);
                                writer.WriteLine(tempContent);
                                writer.WriteLine();

                                // We can delete the temp file after using it
                                File.Delete(tempFile);
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"Error reading temporary file: {ex.Message}");
                            }
                        }
                    }
                }

                // Get a plugin-specific logger
                var pluginLogger = GetPluginLogger(pluginName);

                // Log minimal information to avoid console spam
                if (isShutdownReport)
                {
                    // For shutdown reports, just log that it was saved
                    string displayPath = GetDisplayPath(filePath);
                    pluginLogger.LogInformation($"Shutdown profiling report saved to {displayPath}");
                }
                else
                {
                    // For regular reports, log some statistics
                    var stats = GetProfilingStatistics(pluginName);
                    string displayPath = GetDisplayPath(filePath);

                    pluginLogger.LogInformation(
                        $"Profiling report saved to {displayPath} - " +
                        $"{stats.TotalProfilingRecords} records, " +
                        $"{stats.TotalWarningCount} warnings, " +
                        $"{stats.TotalErrorCount} errors"
                    );
                }

                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving final profiling report for {pluginName}");
                return null;
            }
        }

        /// <summary>
        /// Extracts a more user-friendly display path from the full path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetDisplayPath(string filePath)
        {
            // Extract only path starting from plugins/
            string displayPath = filePath;
            int pluginsIndex = filePath.IndexOf("plugins/");

            if (pluginsIndex >= 0)
                displayPath = filePath.Substring(pluginsIndex);

            return displayPath;
        }

        /// <summary>
        /// Disposes the profiler.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the profiler.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="SdkProfiler"/> class.
        /// </summary>
        ~SdkProfiler()
        {
            Dispose(false);
        }
    }
}