using System.Runtime.CompilerServices;
using Kitsune.SDK.Services.Profiling.Core;

namespace Kitsune.SDK.Services.Profiling.Runtime
{
    /// <summary>
    /// Provides wrapper methods for profiling with advanced features.
    /// </summary>
    public static class ProfiledMethodWrapper
    {
        /// <summary>
        /// Creates a profiling scope with full features.
        /// </summary>
        public static ProfilingScope BeginScope(string pluginName, double warningMs = 0, double criticalMs = 0, long warningBytes = 0, long criticalBytes = 0, bool trackMemory = false, string? description = null, [CallerMemberName] string methodName = "")
        {
            var profiler = SdkProfiler.Instance;
            if (!profiler.IsEnabled)
            {
                // Return a no-op scope
                return new ProfilingScope(profiler, methodName, pluginName, double.MaxValue, double.MaxValue, long.MaxValue, long.MaxValue, false, description);
            }

            var finalWarningMs = warningMs > 0 ? warningMs : 10.0;  // 10ms warning threshold
            var finalCriticalMs = criticalMs > 0 ? criticalMs : 20.0;  // 20ms critical threshold
            var finalWarningBytes = warningBytes > 0 ? warningBytes : profiler.Configuration.MemoryUsageThresholdBytes;
            var finalCriticalBytes = criticalBytes > 0 ? criticalBytes : finalWarningBytes * 2;
            
            return new ProfilingScope(profiler, methodName, pluginName, finalWarningMs, finalCriticalMs, finalWarningBytes, finalCriticalBytes, trackMemory, description);
        }

        /// <summary>
        /// Wraps an action with basic profiling.
        /// </summary>
        public static void Execute(Action action, string pluginName, double warningMs = 0, double criticalMs = 0, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, methodName: methodName);
            try
            {
                action();
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wraps a function with basic profiling.
        /// </summary>
        public static T Execute<T>(Func<T> func, string pluginName, double warningMs = 0, double criticalMs = 0, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, methodName: methodName);
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wraps an async task with profiling.
        /// </summary>
        public static async Task ExecuteAsync(Func<Task> func, string pluginName, double warningMs = 0, double criticalMs = 0, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, methodName: methodName);
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wraps an async function with profiling.
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(Func<Task<T>> func, string pluginName, double warningMs = 0, double criticalMs = 0, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, methodName: methodName);
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wraps an action with both execution and memory profiling.
        /// </summary>
        public static void ExecuteWithMemory(Action action, string pluginName, double warningMs = 0, double criticalMs = 0, long warningBytes = 0, long criticalBytes = 0, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, warningBytes, criticalBytes, trackMemory: true, methodName: methodName);
            try
            {
                action();
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wraps an action with profiling and custom scope configuration.
        /// </summary>
        public static void ExecuteWithScope(Action<ProfilingScope> action, string pluginName, double warningMs = 0, double criticalMs = 0, long warningBytes = 0, long criticalBytes = 0, bool trackMemory = false, string? description = null, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, warningBytes, criticalBytes, trackMemory, description, methodName);
            try
            {
                action(scope);
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }

        /// <summary>
        /// Wraps a function with profiling and custom scope configuration.
        /// </summary>
        public static T ExecuteWithScope<T>(Func<ProfilingScope, T> func, string pluginName, double warningMs = 0, double criticalMs = 0, long warningBytes = 0, long criticalBytes = 0, bool trackMemory = false, string? description = null, [CallerMemberName] string methodName = "")
        {
            using var scope = BeginScope(pluginName, warningMs, criticalMs, warningBytes, criticalBytes, trackMemory, description, methodName);
            try
            {
                return func(scope);
            }
            catch (Exception ex)
            {
                scope.WithException(ex);
                throw;
            }
        }
    }
}