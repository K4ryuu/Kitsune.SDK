using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Services.Profiling.Runtime;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Kitsune.Examples
{
    /// <summary>
    /// Example demonstrating the new profiling system
    /// </summary>
    [MinimumApiVersion(300)]
    [MinimumSdkVersion(1)]
    public class ProfilingExample : SdkPlugin
    {
        public override string ModuleName => "Kitsune Profiling Example";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "K4ryuu @ kitsune-lab.com";
        public override string ModuleDescription => "Example demonstrating new profiling functionality";

        private readonly ConcurrentDictionary<ulong, PlayerData> _playerData = new();
        private readonly Random _random = new();
        private static readonly List<byte[]> _memoryLeakStorage = [];

        protected override void SdkLoad(bool hotReload)
        {
            Profile(() =>
            {
                Logger.LogInformation("🚀 Loading ProfilingExample with profiling!");
                Logger.LogInformation("📊 Using the new wrapper API with steps support!");
                Logger.LogInformation("🔧 Manual profiling for CSS compatibility");

                RegisterCommands();
                InitializeData();

                // Start background timer
                AddTimer(5.0f, BackgroundTask);
            });
        }

        private void RegisterCommands()
        {
            Profile(() =>
            {
                Commands.Register("prof_test", "Test profiling functionality", HandleProfileTestCommand);
                Commands.Register("prof_report", "Show profiling report", HandleProfileReportCommand);
                Commands.Register("prof_clear", "Clear profiling data", HandleProfileClearCommand);
                Commands.Register("prof_memory", "Test memory profiling", HandleMemoryTestCommand);
                Commands.Register("prof_slow", "Simulate slow operation", HandleSlowOperationCommand);
                Commands.Register("prof_leak", "Create memory leak", HandleMemoryLeakCommand);
                Commands.Register("prof_features", "Show profiling features", HandleFeaturesCommand);
                Commands.Register("prof_steps", "Demonstrate step profiling", HandleStepsCommand);
            });
        }

        // This method demonstrates manual profiling
        private void InitializeData()
        {
            Profile(() =>
            {
                // Simulate some initialization work
                Thread.Sleep(30);

                Logger.LogInformation("Data initialization completed!");
            });
        }

        // Command handlers demonstrating different profiling scenarios
        private void HandleProfileTestCommand(CCSPlayerController? controller, CommandInfo info)
        {
            Profile(() =>
            {
                info.ReplyToCommand($" {ChatColors.Gold}=== PROFILING TEST ===");
                info.ReplyToCommand($" {ChatColors.Green}Profiling Status: {ProfilingEnabled}");

                // CPU intensive operation - profiled
                long result = 0;
                for (int i = 0; i < 1000000; i++)
                {
                    result += i * i;
                }

                // Add delay to trigger warning
                Thread.Sleep(50);

                info.ReplyToCommand($" {ChatColors.Yellow}CPU Test Result: {result}");

                // Memory allocation - profiled
                var data = new List<string>();
                for (int i = 0; i < 5000; i++)
                {
                    data.Add($"Test data item {i} with extra content to use more memory");
                }

                info.ReplyToCommand($" {ChatColors.Yellow}Memory Test: Created {data.Count} items");

                info.ReplyToCommand($" {ChatColors.Green}All tests completed! Check console for profiling output.");
            });
        }

        private void HandleProfileReportCommand(CCSPlayerController? controller, CommandInfo info)
        {
            var stats = Profiler.GetProfilingStatistics(ModuleName);

            if (stats == null || stats.TotalProfilingRecords == 0)
            {
                info.ReplyToCommand($" {ChatColors.Red}No profiling data available! Run !prof_test first.");
                return;
            }

            // Generate and save report to file
            Profiler.SaveFinalProfilingReport(ModuleName);

            info.ReplyToCommand($" {ChatColors.Gold}╔══════════════════════════════════════╗");
            info.ReplyToCommand($" {ChatColors.Gold}║{ChatColors.White}    PROFILING REPORT SUMMARY         {ChatColors.Gold}║");
            info.ReplyToCommand($" {ChatColors.Gold}╚══════════════════════════════════════╝");

            info.ReplyToCommand($" {ChatColors.Green}Plugin: {ChatColors.White}{stats.PluginName}");
            info.ReplyToCommand($" {ChatColors.Green}Duration: {ChatColors.White}{stats.Duration.TotalMinutes:F1} minutes");
            info.ReplyToCommand($" {ChatColors.Green}Total Records: {ChatColors.White}{stats.TotalProfilingRecords}");

            // Method execution stats
            if (stats.MethodExecutionCount > 0)
            {
                info.ReplyToCommand($" ");
                info.ReplyToCommand($" {ChatColors.Yellow}📊 Method Execution:");
                info.ReplyToCommand($" {ChatColors.White}  Total: {stats.MethodExecutionCount}");
                info.ReplyToCommand($" {ChatColors.White}  Average: {stats.AverageMethodExecutionTimeMs:F2}ms");
                info.ReplyToCommand($" {ChatColors.White}  Max: {stats.MaxMethodExecutionTimeMs:F2}ms");
                info.ReplyToCommand($" {ChatColors.Orange}  Warnings: {stats.MethodExecutionWarningCount}");
                info.ReplyToCommand($" {ChatColors.Red}  Errors: {stats.MethodExecutionErrorCount}");
            }

            // Memory stats
            if (stats.MemoryProfilingCount > 0)
            {
                info.ReplyToCommand($" ");
                info.ReplyToCommand($" {ChatColors.Yellow}💾 Memory Usage:");
                info.ReplyToCommand($" {ChatColors.White}  Operations: {stats.MemoryProfilingCount}");
                info.ReplyToCommand($" {ChatColors.White}  Total Delta: {stats.TotalMemoryDeltaBytes / (1024.0 * 1024.0):F2} MB");
                info.ReplyToCommand($" {ChatColors.White}  Max Delta: {stats.MaxMemoryDeltaBytes / (1024.0 * 1024.0):F2} MB");
                info.ReplyToCommand($" {ChatColors.Orange}  Warnings: {stats.MemoryWarningCount}");
                info.ReplyToCommand($" {ChatColors.Red}  Errors: {stats.MemoryErrorCount}");
            }

            // Top slow methods
            if (stats.TopSlowMethods.Count > 0)
            {
                info.ReplyToCommand($" ");
                info.ReplyToCommand($" {ChatColors.Gold}🐌 Top Slow Methods:");
                foreach (var method in stats.TopSlowMethods.Take(3))
                {
                    var color = method.ExecutionTimeMs > 50 ? ChatColors.Red : method.ExecutionTimeMs > 25 ? ChatColors.Orange : ChatColors.Green;
                    info.ReplyToCommand($" {color}  {method.MethodName}: {method.ExecutionTimeMs:F2}ms ({method.CallCount} calls)");
                }
            }

            info.ReplyToCommand($" ");
            info.ReplyToCommand($" {ChatColors.Yellow}📁 Detailed report saved to: {ModuleDirectory}/profiling/");
        }

        private void HandleProfileClearCommand(CCSPlayerController? controller, CommandInfo info)
        {
            Profiler.ClearProfilingData(ModuleName);
            info.ReplyToCommand($" {ChatColors.Green}✅ Profiling data cleared!");
        }

        private void HandleMemoryTestCommand(CCSPlayerController? controller, CommandInfo info)
        {
            ProfileWithMemory(() =>
            {
                info.ReplyToCommand($" {ChatColors.Yellow}🧪 Running memory allocation test...");

                // Memory allocation
                // Allocate significant memory
                var largeArray = new byte[5 * 1024 * 1024]; // 5MB
                _random.NextBytes(largeArray);

                // Force garbage collection to see memory behavior
                GC.Collect();
                GC.WaitForPendingFinalizers();

                info.ReplyToCommand($" {ChatColors.Green}Allocated 5MB of memory (temporarily)");

                info.ReplyToCommand($" {ChatColors.Yellow}Check console for memory profiling results!");
            });
        }

        private void HandleSlowOperationCommand(CCSPlayerController? controller, CommandInfo info)
        {
            Profile(() =>
            {
                info.ReplyToCommand($" {ChatColors.Yellow}⏳ Simulating slow operation...");

                // This will trigger profiling warnings
                SlowPrivateMethod();
                SlowMethod();

                info.ReplyToCommand($" {ChatColors.Green}Slow operations completed! Check console for warnings.");
            });
        }

        private void HandleMemoryLeakCommand(CCSPlayerController? controller, CommandInfo info)
        {
            ProfileWithMemory(() =>
            {
                info.ReplyToCommand($" {ChatColors.Red}⚠️ Creating memory leak (1MB)...");

                // Memory leak creation
                // Create a memory leak by storing in static collection
                var leakedData = new byte[1024 * 1024]; // 1MB
                _random.NextBytes(leakedData);
                _memoryLeakStorage.Add(leakedData);

                info.ReplyToCommand($" {ChatColors.Red}Memory leaked! Total leaks: {_memoryLeakStorage.Count} MB");
                info.ReplyToCommand($" {ChatColors.Yellow}Use plugin reload to clear leaked memory");
            });
        }

        private void HandleFeaturesCommand(CCSPlayerController? controller, CommandInfo info)
        {
            info.ReplyToCommand($" {ChatColors.Gold}🎯 PROFILING FEATURES:");
            info.ReplyToCommand($" {ChatColors.Green}✅ Manual profiling wrapper API");
            info.ReplyToCommand($" {ChatColors.Green}✅ Steps support with metadata");
            info.ReplyToCommand($" {ChatColors.Green}✅ Checkpoints for progress tracking");
            info.ReplyToCommand($" {ChatColors.Green}✅ Real-time console output");
            info.ReplyToCommand($" {ChatColors.Green}✅ Memory tracking (optional)");
            info.ReplyToCommand($" {ChatColors.Green}✅ Exception handling");
            info.ReplyToCommand($" {ChatColors.Green}✅ Async methods supported");
            info.ReplyToCommand($" {ChatColors.Green}✅ Fluent API design");
            info.ReplyToCommand($" {ChatColors.Green}✅ Automatic unload reports");
        }

        // Private method - manually profiled
        private void SlowPrivateMethod()
        {
            Profile(() =>
            {
                Thread.Sleep(100); // This will trigger warnings
                Logger.LogInformation("Slow private method completed!");
            });
        }

        // Slow method - manually profiled
        private void SlowMethod()
        {
            ProfiledMethodWrapper.Execute(() =>
            {
                Thread.Sleep(75);
                Logger.LogInformation("Static method completed!");
            }, "Kitsune Profiling Example", methodName: nameof(SlowMethod));
        }

        // Background task to generate some profiling data
        private void BackgroundTask()
        {
            // Random work simulation
            int workType = _random.Next(1, 4);

            switch (workType)
            {
                case 1:
                    ProcessPlayerData();
                    break;
                case 2:
                    PerformMaintenanceTask();
                    break;
                case 3:
                    UpdateStatistics();
                    break;
            }
        }

        private void ProcessPlayerData()
        {
            // Simulate player data processing
            foreach (var player in _playerData.Values)
            {
                player.LastActivity = DateTime.UtcNow;
                Thread.Sleep(2); // Small delay
            }
        }

        private void PerformMaintenanceTask()
        {
            // Simulate maintenance work - automatically profiled by [ProfileAll]
            var tempData = new Dictionary<string, object>();
            for (int i = 0; i < 1000; i++)
            {
                tempData[$"key_{i}"] = $"value_{i}_{Guid.NewGuid()}";
            }

            Thread.Sleep(15);
        }

        private void UpdateStatistics()
        {
            // Simulate statistics update
            Thread.Sleep(_random.Next(10, 40));
        }

        private void HandleStepsCommand(CCSPlayerController? controller, CommandInfo info)
        {
            info.ReplyToCommand($" {ChatColors.Gold}🎯 Running step profiling demo...");

            ProfileWithSteps(scope =>
            {
                scope.Step("Initialization")
                    .WithMetadata("player", controller?.PlayerName ?? "CONSOLE")
                    .WithMetadata("timestamp", DateTime.UtcNow);

                Thread.Sleep(15);

                scope.Step("Database Operations")
                    .WithMetadata("operation", "load_player_data");

                // Simulate multiple database queries
                for (int i = 0; i < 3; i++)
                {
                    Thread.Sleep(10);
                    scope.Checkpoint($"Query {i + 1}/3 completed");
                }

                scope.Step("Processing")
                    .WithMetadata("dataSize", "1024KB");

                // Simulate data processing
                var data = new List<int>();
                for (int i = 0; i < 10000; i++)
                {
                    data.Add(i * i);
                }

                scope.Step("Finalization")
                    .WithMetadata("results", data.Count);

                Thread.Sleep(5);

                info.ReplyToCommand($" {ChatColors.Green}✅ Step profiling completed!");
                info.ReplyToCommand($" {ChatColors.Yellow}Check console for detailed timing breakdown");

            }, warningMs: 50, criticalMs: 100, trackMemory: true, description: "Step profiling demonstration");
        }

        protected override void SdkUnload(bool hotReload)
        {
            Logger.LogInformation("🔄 ProfilingExample unloading...");
            Logger.LogInformation("📊 Profiling report will be automatically generated!");

            // Clear memory leaks on unload
            _memoryLeakStorage.Clear();

            // The base SdkPlugin will automatically:
            // 1. Generate a profiling report
            // 2. Save it to {ModuleDirectory}/profiling/
            // 3. Log summary statistics to console
        }

        // Simple data class for demonstration
        private class PlayerData
        {
            public string Name { get; set; } = "";
            public DateTime ConnectTime { get; set; }
            public DateTime LastActivity { get; set; }
        }
    }

}