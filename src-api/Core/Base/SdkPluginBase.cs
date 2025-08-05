using System.Reflection;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Attributes.Version;
using Kitsune.SDK.Core.Models.Events.Args;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Services.Events;
using Kitsune.SDK.Services.Profiling.Core;
using Kitsune.SDK.Services.Profiling.Runtime;
using Kitsune.SDK.Services.Config;
using Kitsune.SDK.Services.Data.Settings;
using Kitsune.SDK.Services.Data.Storage;
using Kitsune.SDK.Services;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Services.Data.Base;
using Kitsune.SDK.Services.Commands;
using Kitsune.SDK.Utilities.Helpers;
using Kitsune.SDK.Extensions.Player;
using Kitsune.SDK.Core.Attributes.Data;

namespace Kitsune.SDK.Core.Base
{
    /// <summary>
    /// Enhanced base plugin class that provides efficient lifecycle management for Zenith SDK
    /// </summary>
    public abstract partial class SdkPlugin : BasePlugin
    {
        /// <summary>
        /// Current version of the Kitsune SDK - will be used for compatibility checks.
        /// </summary>
        public static int SdkVersion => 1;

        // Resource managers
        private PluginLifecycleService? _lifecycle;
        private CommandHandler? _commands;
        private ConfigHandler? _config;
        private SettingsHandler? _settings;
        private StorageHandler? _storage;
        private PlaceholderHandler? _placeholders;
        private SdkEventManager? _events;

        /// <summary>
        /// Gets whether profiling is enabled for this plugin.
        /// </summary>
        public bool ProfilingEnabled => Profiler.IsEnabled;

        /// <summary>
        /// Gets the profiler instance for this plugin.
        /// </summary>
        public static SdkProfiler Profiler => SdkProfiler.Instance;

        #region Profiling Helper Methods

        /// <summary>
        /// Creates a profiling scope with full control over steps and metadata.
        /// </summary>
        protected ProfilingScope BeginProfileScope(double warningMs = 0, double criticalMs = 0, bool trackMemory = false, string? description = null, [CallerMemberName] string methodName = "")
        {
            return ProfiledMethodWrapper.BeginScope(ModuleName, warningMs, criticalMs, trackMemory: trackMemory, description: description, methodName: methodName);
        }

        /// <summary>
        /// Executes an action with basic profiling.
        /// </summary>
        protected void Profile(Action action, [CallerMemberName] string methodName = "")
        {
            ProfiledMethodWrapper.Execute(action, ModuleName, methodName: methodName);
        }

        /// <summary>
        /// Executes a function with basic profiling.
        /// </summary>
        protected T Profile<T>(Func<T> func, [CallerMemberName] string methodName = "")
        {
            return ProfiledMethodWrapper.Execute(func, ModuleName, methodName: methodName);
        }

        /// <summary>
        /// Executes an async task with profiling.
        /// </summary>
        protected Task ProfileAsync(Func<Task> func, [CallerMemberName] string methodName = "")
        {
            return ProfiledMethodWrapper.ExecuteAsync(func, ModuleName, methodName: methodName);
        }

        /// <summary>
        /// Executes an async function with profiling.
        /// </summary>
        protected Task<T> ProfileAsync<T>(Func<Task<T>> func, [CallerMemberName] string methodName = "")
        {
            return ProfiledMethodWrapper.ExecuteAsync(func, ModuleName, methodName: methodName);
        }

        /// <summary>
        /// Executes an action with both execution and memory profiling.
        /// </summary>
        protected void ProfileWithMemory(Action action, [CallerMemberName] string methodName = "")
        {
            ProfiledMethodWrapper.ExecuteWithMemory(action, ModuleName, methodName: methodName);
        }

        /// <summary>
        /// Executes an action with profiling and access to the scope for steps.
        /// </summary>
        protected void ProfileWithSteps(Action<ProfilingScope> action, double warningMs = 0, double criticalMs = 0, bool trackMemory = false, string? description = null, [CallerMemberName] string methodName = "")
        {
            ProfiledMethodWrapper.ExecuteWithScope(action, ModuleName, warningMs, criticalMs, trackMemory: trackMemory, description: description, methodName: methodName);
        }

        /// <summary>
        /// Executes a function with profiling and access to the scope for steps.
        /// </summary>
        protected T ProfileWithSteps<T>(Func<ProfilingScope, T> func, double warningMs = 0, double criticalMs = 0, bool trackMemory = false, string? description = null, [CallerMemberName] string methodName = "")
        {
            return ProfiledMethodWrapper.ExecuteWithScope(func, ModuleName, warningMs, criticalMs, trackMemory: trackMemory, description: description, methodName: methodName);
        }

        #endregion

        #region Resource Properties with Lazy Initialization

        /// <summary>
        /// Lifecycle service for managing resource disposal (lazy initialized)
        /// </summary>
        protected PluginLifecycleService Lifecycle =>
            _lifecycle ??= new PluginLifecycleService(this);

        /// <summary>
        /// Command handler (automatically created and registered on first use)
        /// </summary>
        public CommandHandler Commands =>
            _commands ??= Lifecycle.RegisterResource(new CommandHandler(this), DisposalPriority.Normal);

        /// <summary>
        /// Config handler (automatically created and registered on first use)
        /// </summary>
        public ConfigHandler Config =>
            _config ??= Lifecycle.RegisterResource(new ConfigHandler(this), DisposalPriority.Last);

        /// <summary>
        /// Settings handler (automatically created and registered on first use)
        /// </summary>
        public SettingsHandler Settings =>
            _settings ??= Lifecycle.RegisterResource(new SettingsHandler(this), DisposalPriority.Late);

        /// <summary>
        /// Storage handler (automatically created and registered on first use)
        /// </summary>
        public StorageHandler Storage =>
            _storage ??= Lifecycle.RegisterResource(new StorageHandler(this), DisposalPriority.Late);

        /// <summary>
        /// Placeholder handler (automatically created and registered on first use)
        /// </summary>
        public PlaceholderHandler Placeholders =>
            _placeholders ??= Lifecycle.RegisterResource(new PlaceholderHandler(this), DisposalPriority.Normal);

        /// <summary>
        /// Event manager (automatically created and registered on first use)
        /// </summary>
        public ISdkEventManager Events
        {
            get
            {
                if (_events != null)
                    return _events;

                // Create event manager and register it with the lifecycle service
                var eventManager = new SdkEventManager(this);
                _events = Lifecycle.RegisterResource(eventManager, DisposalPriority.Normal);
                return _events;
            }
        }

        #endregion

        /// <summary>
        /// DO NOT OVERRIDE! Use SdkLoad instead.
        /// </summary>
        public sealed override void Load(bool hotReload)
        {
            try
            {
                // Fast SDK version check
                CheckSdkVersion();

                // Initialize translation system early to ensure translation files are created
                _ = SdkTranslations.Instance;

                // Set current plugin context for helpers
                PlayerHandlerHelpers.SetPluginContext(this);

                // Initialize profiling if enabled
                if (ProfilingEnabled)
                {
                    try
                    {
                        // Register plugin directory for profiling output
                        // ModulePath is the DLL path, so we need to get the directory
                        string pluginDirectory = Path.GetDirectoryName(ModulePath) ?? ModulePath;
                        Profiler.RegisterPluginDirectory(ModuleName, pluginDirectory);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to initialize profiling for {ModuleName}, continuing without profiling", ModuleName);
                    }
                }

                // Start async loading sequence without blocking the main thread
                CSSThread.RunOnMainThread(async () =>
                {
                    try
                    {
                        // 1. Initialize configuration first - this may be needed for other systems
                        bool hasConfigInterface = GetType().GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkConfig<>));

                        bool hasStorageInterface = GetType().GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkStorage<>));

                        bool hasSettingsInterface = GetType().GetInterfaces().Any(i =>
                            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkSettings<>));

                        if (hasConfigInterface)
                        {
                            await InitializeConfigAsync(hasStorageInterface, hasSettingsInterface);
                        }

                        // 2. Initialize Storage and Settings handlers if needed
                        if (hasStorageInterface || hasSettingsInterface)
                        {
                            if (!Config.IsDatabaseValid)
                                throw new InvalidOperationException("Database is not configured correctly. Unloading plugin...");

                            // Setup Storage handler
                            if (hasStorageInterface)
                            {
                                await InitializeStorageHandlerAsync();
                            }

                            // Setup Settings handler
                            if (hasSettingsInterface)
                            {
                                await InitializeSettingsHandlerAsync();
                            }
                        }

                        var args = new ModuleLoadEventArgs(ModuleName, ModuleVersion, ModulePath);
                        Events.Dispatch(args, HookMode.Pre);

                        // 3. Call SdkLoad when all systems are initialized
                        SdkLoad(hotReload);

                        Events.Dispatch(args, HookMode.Post);

                        // 4. Initialize data for online players only for hotReload
                        if (hotReload && (hasStorageInterface || hasSettingsInterface))
                        {
                            // Delay player data initialization to allow plugin to fully initialize
                            // This ensures storage properties are registered before loading data
                            InitializeOnlinePlayerData();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Error in loading sequence: {ErrorMessage}", ex.Message);
                        PluginManager.UnloadContext(Path.GetFileNameWithoutExtension(ModulePath));
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// DO NOT OVERRIDE! Use SdkUnload instead.
        /// </summary>
        public sealed override void Unload(bool hotReload)
        {
            try
            {
                // Fire pre unload event if events were initialized
                if (_events != null)
                {
                    var args = new ModuleUnloadEventArgs(ModuleName, hotReload ? "hotReload" : "unload");
                    _events.Dispatch(args, HookMode.Pre);
                }

                // IMPORTANT: Save all online player data BEFORE disposing anything
                SaveAllOnlinePlayerDataBeforeUnload();

                // IMPORTANT: Dispose all players FIRST (before handlers)
                DisposeAllPlayers();

                // Call the SDK-specific unload method
                SdkUnload(hotReload);

                // Fire post unload event if events were initialized
                if (_events != null)
                {
                    var args = new ModuleUnloadEventArgs(ModuleName, hotReload ? "hotReload" : "unload");
                    _events.Dispatch(args, HookMode.Post);
                }

                // Handle profiling data if enabled
                SaveProfilingDataOnUnload();

                // Automatically dispose all registered resources in priority order
                _lifecycle?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to unload {PluginName}", ModuleName);
            }
        }

        /// <summary>
        /// Save all online player data before unloading to prevent data loss
        /// </summary>
        private void SaveAllOnlinePlayerDataBeforeUnload()
        {
            try
            {
                // Only save if we have storage or settings handlers
                if (_storage != null || _settings != null)
                {
                    // Use direct database operations without CSS thread scheduling to avoid deadlock
                    var saveTask = Task.Run(async () =>
                    {
                        try
                        {
                            var saveTasks = new List<Task>();

                            if (_storage != null)
                            {
                                saveTasks.Add(_storage.SaveAllOnlinePlayerDataAsync());
                            }

                            if (_settings != null)
                            {
                                saveTasks.Add(_settings.SaveAllOnlinePlayerDataAsync());
                            }

                            // Wait for all saves to complete
                            await Task.WhenAll(saveTasks);

                            Logger.LogInformation("Successfully saved all player data during unload for {Plugin}", ModuleName);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error saving player data during unload for {Plugin}", ModuleName);
                            throw;
                        }
                    });

                    // Wait synchronously with timeout
                    if (!saveTask.Wait(TimeSpan.FromSeconds(3)))
                    {
                        Logger.LogWarning("Timeout while saving player data during unload for {Plugin}", ModuleName);
                    }
                    else if (saveTask.Exception != null)
                    {
                        Logger.LogError(saveTask.Exception, "Error in save task for {Plugin}", ModuleName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in SaveAllOnlinePlayerDataBeforeUnload for {Plugin}", ModuleName);
            }
        }

        /// <summary>
        /// Dispose all online players before unloading handlers
        /// </summary>
        private void DisposeAllPlayers()
        {
            try
            {
                foreach (var player in Player.ValidLoop())
                {
                    try
                    {
                        player.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error disposing player {Name} ({SteamId})", player.Name, player.SteamID);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error disposing players");
            }
        }

        #region Async Initialization Methods

        /// <summary>
        /// Initialize the configuration handler - simplified without DatabaseConnectionManager
        /// </summary>
        private async Task InitializeConfigAsync(bool hasStorageInterface, bool hasSettingsInterface)
        {
            try
            {
                // Make sure the config handler is created
                var configHandler = Config;

                // Initialize the config instance if this plugin implements ISdkConfig
                await configHandler.InitializeConfigAsync(hasStorageInterface, hasSettingsInterface);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing configuration for {PluginName}", ModuleName);
                throw;
            }
        }

        /// <summary>
        /// Initialize the storage handler
        /// </summary>
        private async Task InitializeStorageHandlerAsync()
        {
            try
            {
                // Make sure the storage handler is created
                _ = Storage;

                // If this plugin implements ISdkStorage, automatically register type binding
                Type? storageInterface = GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkStorage<>));

                if (storageInterface != null)
                {
                    // Get the storage type from the interface
                    Type storageType = storageInterface.GetGenericArguments()[0];

                    // Create a prototype instance to register its attributes
                    object prototype = Activator.CreateInstance(storageType)!;

                    // Execute the storage initialization as a task
                    await Task.Run(() =>
                    {
                        // Find properties with StorageAttribute and register defaults
                        var defaults = new Dictionary<string, object?>();

                        foreach (var property in storageType.GetProperties())
                        {
                            StorageAttribute? attr = property.GetCustomAttribute<StorageAttribute>();
                            if (attr != null)
                            {
                                object? defaultValue = property.GetValue(prototype);
                                defaults[attr.Name] = defaultValue;
                            }
                        }

                        // Register all defaults at once
                        if (defaults.Count > 0)
                        {
                            Storage.Register(defaults);
                        }
                    });

                    // Initialize database table and columns
                    await Storage.InitializeDatabaseStructureAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing storage handler for {PluginName}: {ErrorMessage}", ModuleName, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Initialize the settings handler
        /// </summary>
        private async Task InitializeSettingsHandlerAsync()
        {
            try
            {
                // Make sure the settings handler is created
                _ = Settings;

                // If this plugin implements ISdkSettings, automatically register type binding
                Type? settingsInterface = GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkSettings<>));

                if (settingsInterface != null)
                {
                    // Get the settings type from the interface
                    Type settingsType = settingsInterface.GetGenericArguments()[0];

                    // Create a prototype instance to register its attributes
                    object prototype = Activator.CreateInstance(settingsType)!;

                    // Execute the settings initialization as a task
                    await Task.Run(() =>
                    {
                        // Find properties with SettingAttribute and register defaults
                        var defaults = new Dictionary<string, object?>();

                        foreach (var property in settingsType.GetProperties())
                        {
                            SettingAttribute? attr = property.GetCustomAttribute<SettingAttribute>();
                            if (attr != null)
                            {
                                object? defaultValue = property.GetValue(prototype);
                                defaults[attr.Name] = defaultValue;
                            }
                        }

                        // Register all defaults at once
                        if (defaults.Count > 0)
                        {
                            Settings.Register(defaults);
                        }
                    });

                    // Initialize database table and columns
                    await Settings.InitializeDatabaseStructureAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing settings handler for {PluginName}", ModuleName);
                throw;
            }
        }

        /// <summary>
        /// Initialize data for all online players - simplified with bulk loading
        /// </summary>
        private void InitializeOnlinePlayerData()
        {
            try
            {
                // Get a list of online players
                var onlinePlayers = PlayerEx.GetValidPlayers().ToList();

                if (onlinePlayers.Count == 0)
                    return;

                // First, create Player objects for all online players without loading data
                var steamIds = new List<ulong>();
                foreach (var controller in onlinePlayers)
                {
                    // This will create the Player object if it doesn't exist
                    // But won't load data because skipDataLoad is true
                    var player = Player.GetOrCreate<Player>(controller, skipDataLoad: true);
                    if (player != null)
                    {
                        steamIds.Add(player.SteamID);
                    }
                }

                // Now bulk load all player data at once for better performance
                if (steamIds.Count > 0)
                {
                    CSSThread.RunOnMainThread(async () =>
                    {
                        await PlayerDataHandler.LoadMultiplePlayersForPluginAsync(steamIds, this);
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing online player data for {PluginName}", ModuleName);
            }
        }

        #endregion

        /// <summary>
        /// Helper method to get a typed config instance for plugins implementing ISdkConfig
        /// </summary>
        protected TConfig GetTypedConfig<TConfig>() where TConfig : class, new()
        {
            // Get the config adapter through the ConfigHandler
            var adapter = Config.GetConfigAdapter<TConfig>();
            return adapter.Instance;
        }

        #region Private Helper Methods

        /// <summary>
        /// Check for SDK version compatibility
        /// </summary>
        private void CheckSdkVersion()
        {
            var minVersionAttr = GetType().GetCustomAttribute<MinimumSdkVersionAttribute>();
            if (minVersionAttr != null)
            {
                int requiredVersion = minVersionAttr.Version;
                if (SdkVersion < requiredVersion)
                {
                    string errorMessage = $"Plugin {ModuleName} requires SDK version {requiredVersion}, but current version is {SdkVersion}";
                    Logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }
            }
        }

        /// <summary>
        /// Save profiling data when plugin unloads
        /// </summary>
        private void SaveProfilingDataOnUnload()
        {
            if (!ProfilingEnabled || !Profiler.IsEnabled)
                return;

            // Check if we have any profiling data worth saving
            var stats = Profiler.GetProfilingStatistics(ModuleName);
            if (stats?.TotalProfilingRecords > 0)
            {
                // Generate a brief summary for the console log
                var pluginLogger = Profiler.GetPluginLogger(ModuleName);
                pluginLogger.LogInformation(
                    $"Final profiling stats: {stats.TotalProfilingRecords} records, " +
                    $"slowest: {stats.MaxMethodExecutionTimeMs:F2}ms, " +
                    $"warnings: {stats.MethodExecutionWarningCount + stats.MemoryWarningCount}, " +
                    $"errors: {stats.MethodExecutionErrorCount + stats.MemoryErrorCount}");

                // Save the final profiling report
                Profiler.SaveFinalProfilingReport(ModuleName, isShutdownReport: true);
            }

            // Clear profiling data for this plugin to avoid memory leaks
            Profiler.ClearProfilingData(ModuleName);
        }

        #endregion

        /// <summary>
        /// Override this method to initialize your plugin (optional)
        /// </summary>
        /// <param name="hotReload">Whether this is a hot reload</param>
        protected virtual void SdkLoad(bool hotReload)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Override this method to clean up your plugin (optional)
        /// </summary>
        /// <param name="hotReload">Whether this is a hot reload</param>
        protected virtual void SdkUnload(bool hotReload)
        {
            // Default implementation does nothing
        }

        /// <summary>
        /// Creates and automatically registers a service for lifecycle management
        /// </summary>
        /// <typeparam name="T">Type of the service</typeparam>
        /// <param name="factory">Factory function to create the service</param>
        /// <returns>The created and registered service</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T CreateService<T>(Func<T> factory) where T : class
        {
            return Lifecycle.RegisterResource(factory());
        }
    }
}