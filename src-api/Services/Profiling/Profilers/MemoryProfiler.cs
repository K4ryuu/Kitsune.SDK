using System.Runtime.CompilerServices;
using Kitsune.SDK.Services.Profiling.Core;
using Kitsune.SDK.Services.Profiling.Data;

namespace Kitsune.SDK.Services.Profiling.Profilers
{
    /// <summary>
    /// Profiler for memory usage
    /// </summary>
    public sealed class MemoryProfiler : IDisposable
    {
        private readonly SdkProfiler _profiler;
        private readonly string _operationName;
        private readonly string _pluginName;
        private readonly long _warningThresholdBytes;
        private readonly long _criticalThresholdBytes;
        private readonly bool _detailedProfilingEnabled;
        private readonly string? _description;
        private readonly bool _captureStackTrace;
        private readonly string _sourceFile;
        private readonly int _sourceLineNumber;
        private readonly string _sourceMemberName;
        private readonly long _memoryBefore;
        private readonly Dictionary<string, long>? _detailedAllocations;
        private bool _disposed;

        /// <summary>
        /// Initializes a new memory profiler
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryProfiler(SdkProfiler profiler, string operationName, string pluginName, long warningThresholdBytes, long criticalThresholdBytes, bool detailedProfilingEnabled, string? description, bool captureStackTrace, string sourceFile, int sourceLineNumber, string sourceMemberName)
        {
            _profiler = profiler;
            _operationName = operationName;
            _pluginName = pluginName;
            _warningThresholdBytes = warningThresholdBytes;
            _criticalThresholdBytes = criticalThresholdBytes;
            _detailedProfilingEnabled = detailedProfilingEnabled;
            _description = description;
            _captureStackTrace = captureStackTrace;
            _sourceFile = sourceFile;
            _sourceLineNumber = sourceLineNumber;
            _sourceMemberName = sourceMemberName;

            // Baseline memory collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            _memoryBefore = GC.GetTotalMemory(true);

            // Initialize collection for detailed memory profiling
            _detailedAllocations = _detailedProfilingEnabled ? [] : null;
        }

        /// <summary>
        /// Disposes the profiler and records memory usage
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // Force collection for accurate measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Get memory after operation
            long memoryAfter = GC.GetTotalMemory(true);
            long memoryDelta = memoryAfter - _memoryBefore;

            // Check for potential memory leaks (heuristic)
            bool potentialLeak = memoryDelta > _warningThresholdBytes * 2;

            // Create profiling data
            var data = new MemoryProfilingData(_operationName, _pluginName, _memoryBefore, memoryAfter, _warningThresholdBytes, _criticalThresholdBytes, _detailedProfilingEnabled, _detailedAllocations, potentialLeak, _description, _captureStackTrace, _sourceFile, _sourceLineNumber, _sourceMemberName);

            // Record the profiling data
            _profiler.RecordProfilingData(data);
        }
    }
}