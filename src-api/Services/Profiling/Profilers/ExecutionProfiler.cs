using System.Diagnostics;
using System.Runtime.CompilerServices;
using Kitsune.SDK.Services.Profiling.Core;
using Kitsune.SDK.Services.Profiling.Data;

namespace Kitsune.SDK.Services.Profiling.Profilers
{
    /// <summary>
    /// Profiler for method execution
    /// </summary>
    public sealed class ExecutionProfiler : IDisposable
    {
        private readonly SdkProfiler _profiler;
        private readonly string _methodName;
        private readonly string _pluginName;
        private readonly double _warningThresholdMs;
        private readonly double _criticalThresholdMs;
        private readonly string? _description;
        private readonly bool _captureStackTrace;
        private readonly string _sourceFile;
        private readonly int _sourceLineNumber;
        private readonly string _sourceMemberName;
        private readonly Stopwatch _stopwatch;
        private Dictionary<string, object>? _arguments;
        private object? _returnValue;
        private Exception? _exception;
        private bool _disposed;

        /// <summary>
        /// Initializes a new execution profiler
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExecutionProfiler(SdkProfiler profiler, string methodName, string pluginName, double warningThresholdMs, double criticalThresholdMs, string? description, bool captureStackTrace, string sourceFile, int sourceLineNumber, string sourceMemberName)
        {
            _profiler = profiler;
            _methodName = methodName;
            _pluginName = pluginName;
            _warningThresholdMs = warningThresholdMs;
            _criticalThresholdMs = criticalThresholdMs;
            _description = description;
            _captureStackTrace = captureStackTrace;
            _sourceFile = sourceFile;
            _sourceLineNumber = sourceLineNumber;
            _sourceMemberName = sourceMemberName;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Sets the method arguments for profiling
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetArguments(Dictionary<string, object> arguments) => _arguments = arguments;

        /// <summary>
        /// Sets the return value for profiling
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetReturnValue(object returnValue) => _returnValue = returnValue;

        /// <summary>
        /// Sets the exception for profiling
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception exception) => _exception = exception;

        /// <summary>
        /// Disposes the profiler and records the execution time
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _stopwatch.Stop();

            var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;

            // Create profiling data
            var data = new MethodExecutionProfilingData(_methodName, _pluginName, elapsedMs, _warningThresholdMs, _criticalThresholdMs, _arguments, _returnValue, _exception, _description, _captureStackTrace, _sourceFile, _sourceLineNumber, _sourceMemberName);

            // Record the profiling data
            _profiler.RecordProfilingData(data);
        }
    }
}