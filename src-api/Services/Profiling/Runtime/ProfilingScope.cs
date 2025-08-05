using System.Diagnostics;
using Kitsune.SDK.Services.Profiling.Core;
using Kitsune.SDK.Services.Profiling.Data;
using Microsoft.Extensions.Logging;

namespace Kitsune.SDK.Services.Profiling.Runtime
{
    /// <summary>
    /// Represents a profiling scope with support for steps and detailed tracking.
    /// </summary>
    public class ProfilingScope : IDisposable
    {
        private readonly SdkProfiler _profiler;
        private readonly string _methodName;
        private readonly string _pluginName;
        private readonly double _warningThresholdMs;
        private readonly double _criticalThresholdMs;
        private readonly long _memoryWarningBytes;
        private readonly long _memoryCriticalBytes;
        private readonly bool _trackMemory;
        private readonly string? _description;
        private readonly Stopwatch _stopwatch;
        private readonly long _startMemory;
        private readonly List<StepInfo> _steps = [];
        private readonly Dictionary<string, object> _metadata = [];

        private bool _disposed;
        private Exception? _exception;
        private StepInfo? _currentStep;

        /// <summary>
        /// Information about a single step within the profiling scope.
        /// </summary>
        private class StepInfo
        {
            public string Name { get; set; } = "";
            public long StartTime { get; set; }
            public long EndTime { get; set; }
            public long MemoryBefore { get; set; }
            public long MemoryAfter { get; set; }
            public string? Description { get; set; }
            public Dictionary<string, object> Metadata { get; set; } = [];
        }

        internal ProfilingScope(SdkProfiler profiler, string methodName, string pluginName, double warningThresholdMs, double criticalThresholdMs, long memoryWarningBytes, long memoryCriticalBytes, bool trackMemory, string? description)
        {
            _profiler = profiler;
            _methodName = methodName;
            _pluginName = pluginName;
            _warningThresholdMs = warningThresholdMs;
            _criticalThresholdMs = criticalThresholdMs;
            _memoryWarningBytes = memoryWarningBytes;
            _memoryCriticalBytes = memoryCriticalBytes;
            _trackMemory = trackMemory;
            _description = description;
            _stopwatch = Stopwatch.StartNew();

            if (_trackMemory)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                _startMemory = GC.GetTotalMemory(false);
            }
        }

        /// <summary>
        /// Starts a new step within this profiling scope.
        /// </summary>
        public ProfilingScope Step(string stepName, string? description = null)
        {
            // End current step if exists
            if (_currentStep != null)
            {
                EndCurrentStep();
            }

            // Start new step
            _currentStep = new StepInfo
            {
                Name = stepName,
                Description = description,
                StartTime = _stopwatch.ElapsedMilliseconds,
                MemoryBefore = _trackMemory ? GC.GetTotalMemory(false) : 0
            };

            var logger = _profiler.GetPluginLogger(_pluginName);
            logger.LogInformation($"[STEP] {_methodName}.{stepName} started");

            return this; // Fluent API
        }

        /// <summary>
        /// Adds metadata to the current step or scope.
        /// </summary>
        public ProfilingScope WithMetadata(string key, object value)
        {
            if (_currentStep != null)
            {
                _currentStep.Metadata[key] = value;
            }
            else
            {
                _metadata[key] = value;
            }
            return this; // Fluent API
        }

        /// <summary>
        /// Marks the current operation as failed with an exception.
        /// </summary>
        public ProfilingScope WithException(Exception exception)
        {
            _exception = exception;
            return this; // Fluent API
        }

        /// <summary>
        /// Logs a checkpoint with current timing information.
        /// </summary>
        public ProfilingScope Checkpoint(string message)
        {
            var elapsed = _stopwatch.ElapsedMilliseconds;
            var logger = _profiler.GetPluginLogger(_pluginName);
            logger.LogInformation($"[CHECKPOINT] {_methodName} @ {elapsed}ms: {message}");
            return this; // Fluent API
        }

        /// <summary>
        /// Ends the current step.
        /// </summary>
        private void EndCurrentStep()
        {
            if (_currentStep == null) return;

            _currentStep.EndTime = _stopwatch.ElapsedMilliseconds;
            if (_trackMemory)
            {
                _currentStep.MemoryAfter = GC.GetTotalMemory(false);
            }

            var stepDuration = _currentStep.EndTime - _currentStep.StartTime;
            var logger = _profiler.GetPluginLogger(_pluginName);

            // Log step completion
            if (stepDuration > _warningThresholdMs / 2) // Half of method threshold for steps
            {
                logger.LogWarning($"[STEP] {_methodName}.{_currentStep.Name} took {stepDuration}ms");
            }
            else
            {
                logger.LogInformation($"[STEP] {_methodName}.{_currentStep.Name} completed in {stepDuration}ms");
            }

            _steps.Add(_currentStep);
            _currentStep = null;
        }

        /// <summary>
        /// Disposes the profiling scope and records all data.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // End current step if exists
            if (_currentStep != null)
            {
                EndCurrentStep();
            }

            _stopwatch.Stop();
            var totalElapsed = _stopwatch.Elapsed.TotalMilliseconds;

            // Create detailed profiling data
            var data = new DetailedProfilingData(_methodName, _pluginName, totalElapsed, _warningThresholdMs, _criticalThresholdMs, _description)
            {
                Exception = _exception,
                Metadata = _metadata,
                Steps = _steps.Select(s => new DetailedProfilingData.StepData
                {
                    Name = s.Name,
                    Duration = s.EndTime - s.StartTime,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    MemoryDelta = s.MemoryAfter - s.MemoryBefore,
                    Description = s.Description,
                    Metadata = s.Metadata
                }).ToList()
            };

            // Add memory data if tracked
            if (_trackMemory)
            {
                var endMemory = GC.GetTotalMemory(false);
                data.MemoryBefore = _startMemory;
                data.MemoryAfter = endMemory;
                data.MemoryDelta = endMemory - _startMemory;
            }

            // Record the profiling data
            _profiler.RecordProfilingData(data);

            // Real-time console output
            LogRealtimeInfo(data);
        }

        /// <summary>
        /// Logs real-time information to console.
        /// </summary>
        private void LogRealtimeInfo(DetailedProfilingData data)
        {
            var logger = _profiler.GetPluginLogger(_pluginName);
            var severity = data.Severity;


            // Build the message
            var message = $"{_methodName} completed in {data.ElapsedMilliseconds:F2}ms";

            if (data.Steps.Any())
            {
                message += $" ({data.Steps.Count} steps)";
            }

            if (_trackMemory && data.MemoryDelta > 0)
            {
                message += $" | Memory: +{data.MemoryDelta / 1024.0:F2}KB";

                // Check memory thresholds
                if (data.MemoryDelta >= _memoryCriticalBytes)
                {
                    message += " [CRITICAL]";
                }
                else if (data.MemoryDelta >= _memoryWarningBytes)
                {
                    message += " [WARNING]";
                }
            }

            if (_exception != null)
            {
                message += $" | FAILED: {_exception.GetType().Name}";
            }

            // Log with appropriate level
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

            // Log detailed steps if warning or error
            if (severity >= LogLevel.Warning && data.Steps.Count != 0)
            {
                foreach (var step in data.Steps.OrderByDescending(s => s.Duration).Take(3))
                {
                    logger.LogWarning($"  ├─ {step.Name}: {step.Duration}ms");
                }
            }
        }
    }
}