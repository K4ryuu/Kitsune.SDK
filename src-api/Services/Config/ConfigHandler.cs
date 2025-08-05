using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Kitsune.SDK.Core.Models.Config;
using Kitsune.SDK.Core.Interfaces;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Attributes.Config;

namespace Kitsune.SDK.Services.Config
{
    /// <summary>
    /// High-performance configuration handler with type-safe access, optimized for CS2 plugins
    /// </summary>
    public sealed partial class ConfigHandler : IDisposable
    {
        #region Static Members

        // Module configuration storage
        private static readonly ConcurrentDictionary<SdkPlugin, ModuleConfig> _moduleConfigs = new();

        // Cache of instantiated config objects
        private static readonly ConcurrentDictionary<(SdkPlugin, Type), object> _configInstances = new();

        // Track if database configs are registered per plugin
        public bool IsDatabaseValid
        {
            get
            {
                try
                {
                    var database = GetLocalValue<string>("database", "database");
                    var username = GetLocalValue<string>("username", "database");
                    var password = GetLocalValue<string>("password", "database");

                    return !string.IsNullOrEmpty(database) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region Instance Fields

        // Reference to the plugin this handler belongs to
        internal readonly SdkPlugin _plugin;

        // Plugin logger
        private readonly ILogger _logger;

        // Config directory path
        private readonly string _configDirectory;

        // Plugin name for file paths
        private readonly string _configOwner;

        // Disposal state flag
        private bool _disposed;

        // Interface informations
        private bool _hasStorageInterface;
        private bool _hasSettingsInterface;

        #endregion

        /// <summary>
        /// Creates a new config handler for the specified plugin
        /// </summary>
        /// <param name="plugin">The plugin this config handler belongs to</param>
        /// <param name="configOwnerOverride">Optional override for config directory name (defaults to plugin name)</param>
        /// <param name="moduleDirectoryOverride">Optional override for module directory (defaults to plugin directory)</param>
        public ConfigHandler(SdkPlugin plugin, string? configOwnerOverride = null, string? moduleDirectoryOverride = null)
        {
            _plugin = plugin;
            _logger = plugin.Logger;

            // Setup config directory - use override if provided, otherwise use plugin name
            _configOwner = configOwnerOverride ?? Path.GetFileNameWithoutExtension(plugin.ModulePath);

            // Calculate module directory with override support
            string moduleDirectory = moduleDirectoryOverride ?? plugin.ModuleDirectory ?? Path.GetDirectoryName(plugin.ModulePath) ?? ".";
            _configDirectory = Path.Combine(moduleDirectory, "..", "..", "configs", "plugins", _configOwner);

            // Ensure directory exists
            Directory.CreateDirectory(_configDirectory);

            // Initialize module config if needed
            _moduleConfigs.TryAdd(plugin, new ModuleConfig { ModuleName = plugin.ModuleName });
        }

        #region Generic Config Implementation

        /// <summary>
        /// Initializes the config instance for a plugin that implements ISdkConfig
        /// </summary>
        public async Task InitializeConfigAsync(bool hasStorageInterface, bool hasSettingsInterface)
        {
            // Determine if the plugin implements ISdkConfig and get the config type
            Type? configInterface = _plugin.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISdkConfig<>));

            _hasStorageInterface = hasStorageInterface;
            _hasSettingsInterface = hasSettingsInterface;

            if (configInterface == null)
                return;

            // Get the config type from the interface
            Type configType = configInterface.GetGenericArguments()[0];

            // Create a new instance of the config type
            object configInstance = Activator.CreateInstance(configType) ??
                throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

            // Register the properties from the config class
            await RegisterConfigPropertiesAsync(configInstance, configType);

            // Store the config instance for later access
            _configInstances[(_plugin, configType)] = configInstance;
        }

        /// <summary>
        /// Registers all properties from a config class that have the ConfigAttribute
        /// </summary>
        private async Task RegisterConfigPropertiesAsync(object configInstance, Type configType)
        {
            // Check if config requires database and handle early validation
            var requiresDbAttribute = configType.GetCustomAttribute<RequiresDatabaseAttribute>();
            bool needsDatabase = requiresDbAttribute != null;

            // Use a HashSet to track which groups we need to process
            var groupsToProcess = new HashSet<string>();

            // Process all properties with the ConfigAttribute or ConfigGroupAttribute
            foreach (PropertyInfo property in configType.GetProperties())
            {
                // Check for ConfigGroup attribute first
                ConfigGroupAttribute? configGroupAttr = property.GetCustomAttribute<ConfigGroupAttribute>();
                if (configGroupAttr != null)
                {
                    // Get the nested config object
                    var nestedConfigInstance = property.GetValue(configInstance);
                    if (nestedConfigInstance == null)
                    {
                        nestedConfigInstance = Activator.CreateInstance(property.PropertyType)
                            ?? throw new InvalidOperationException($"Failed to create instance of {property.PropertyType.Name}");

                        property.SetValue(configInstance, nestedConfigInstance);
                    }

                    // Process all properties in the nested config
                    foreach (PropertyInfo nestedProperty in property.PropertyType.GetProperties())
                    {
                        ConfigAttribute? nestedConfigAttr = nestedProperty.GetCustomAttribute<ConfigAttribute>();
                        if (nestedConfigAttr == null)
                            continue;

                        // Get the default value from the nested property
                        var nestedDefaultValue = nestedProperty.GetValue(nestedConfigInstance);
                        var nestedType = nestedProperty.PropertyType;

                        // Use the nested property's default value or create a new instance if needed
                        var nestedValue = nestedDefaultValue
                            ?? Activator.CreateInstance(nestedType)
                            ?? throw new InvalidOperationException($"Failed to create default value for {nestedType.Name}");

                        // Register this config using the group name from ConfigGroupAttribute
                        Register(configGroupAttr.Name, nestedConfigAttr.Name, nestedConfigAttr.Description, nestedValue, nestedConfigAttr.Flags);

                        // Track which group this belongs to for later processing
                        groupsToProcess.Add(configGroupAttr.Name);
                    }
                    continue;
                }

                // Check for regular ConfigAttribute
                ConfigAttribute? configAttr = property.GetCustomAttribute<ConfigAttribute>();
                if (configAttr == null)
                    continue;

                // Get the default value from the property
                var defaultValue = property.GetValue(configInstance);
                var type = property.PropertyType;

                // Use the property's default value or create a new instance if needed
                var value = defaultValue
                    ?? Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"Failed to create default value for {type.Name}");

                // Register this config directly (only adds to memory)
                Register(configAttr.GroupName, configAttr.Name, configAttr.Description, value, configAttr.Flags);

                // Track which group this belongs to for later processing
                groupsToProcess.Add(configAttr.GroupName);
            }

            if (_hasStorageInterface || _hasSettingsInterface || needsDatabase)
            {
                // Register standard database configs if needed
                RegisterDatabaseConfigs();

                groupsToProcess.Add("database");

                if (_hasStorageInterface)
                {
                    Register("database", "storage_table", "Table name for player storage data. The same can be used for all plugins optionally.", "kitsune_player_storage", ConfigFlag.Locked);
                }

                if (_hasSettingsInterface)
                {
                    Register("database", "settings_table", "Table name for player settings data. The same can be used for all plugins optionally.", "kitsune_player_settings", ConfigFlag.Locked);
                }
            }

            // Check which groups have existing config files and which need to be created
            var existingGroupFiles = new HashSet<string>();
            var missingGroupFiles = new HashSet<string>();

            // Determine which config files exist and which need to be created
            foreach (var groupName in groupsToProcess)
            {
                string filePath = GetGroupConfigFilePath(groupName);
                if (File.Exists(filePath))
                {
                    existingGroupFiles.Add(groupName);
                }
                else
                {
                    missingGroupFiles.Add(groupName);
                }
            }

            // For existing config files, load them and update our in-memory values
            foreach (var groupName in existingGroupFiles)
            {
                // Load existing config values from file
                await LoadGroupConfig(groupName);
            }

            // For missing config files, create them with default values
            foreach (var groupName in missingGroupFiles)
            {
                // Create the config file with current in-memory values
                await SaveGroupConfig(groupName, _plugin);
            }

            // Now update the config instance with values from the loaded files
            UpdateConfigInstanceFromConfig(configInstance, configType);

            // Validate database if required and not optional
            if (needsDatabase && !requiresDbAttribute!.Optional)
            {
                await ValidateDatabaseRequirement(requiresDbAttribute);
            }
            else if (needsDatabase && requiresDbAttribute?.Optional == true)
            {
                _logger.LogInformation("Database configuration created for plugin '{PluginName}' (optional database support)", _plugin.ModuleName);
            }
        }

        /// <summary>
        /// Validates database requirement and logs appropriate messages
        /// </summary>
        private async Task ValidateDatabaseRequirement(RequiresDatabaseAttribute requiresDbAttribute)
        {
            try
            {
                if (!IsDatabaseValid)
                {
                    var message = !string.IsNullOrEmpty(requiresDbAttribute.FailureMessage)
                        ? requiresDbAttribute.FailureMessage
                        : $"Plugin '{_plugin.ModuleName}' requires database configuration but database settings are invalid.";

                    _logger.LogWarning("{Message}", message);
                    _logger.LogWarning("Please configure database settings: host, port, username, password, database");

                    if (requiresDbAttribute.FailOnInvalidDatabase)
                    {
                        throw new InvalidOperationException($"Database validation failed for plugin '{_plugin.ModuleName}': {message}");
                    }
                }
                else
                {
                    try
                    {
                        var connectionString = GetConnectionString();
                        using var connection = new MySqlConnection(connectionString);
                        await connection.OpenAsync();
                        await connection.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Database connection test failed for plugin '{PluginName}': {ErrorMessage}", _plugin.ModuleName, ex.Message);

                        if (requiresDbAttribute.FailOnInvalidDatabase)
                        {
                            throw new InvalidOperationException($"Database connection test failed for plugin '{_plugin.ModuleName}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex) when (!requiresDbAttribute.FailOnInvalidDatabase)
            {
                _logger.LogWarning("Database validation failed for plugin '{PluginName}': {ErrorMessage}", _plugin.ModuleName, ex.Message);
            }
        }

        /// <summary>
        /// Update config instance properties with values from the loaded configuration
        /// </summary>
        private void UpdateConfigInstanceFromConfig(object configInstance, Type configType)
        {
            // Iterate through properties and update from loaded config values
            foreach (var property in configType.GetProperties())
            {
                // Check for ConfigGroup attribute first
                ConfigGroupAttribute? configGroupAttr = property.GetCustomAttribute<ConfigGroupAttribute>();
                if (configGroupAttr != null)
                {
                    // Get the nested config object
                    var nestedConfigInstance = property.GetValue(configInstance);
                    if (nestedConfigInstance == null)
                    {
                        nestedConfigInstance = Activator.CreateInstance(property.PropertyType)
                            ?? throw new InvalidOperationException($"Failed to create instance of {property.PropertyType.Name}");

                        property.SetValue(configInstance, nestedConfigInstance);
                    }

                    // Update all properties in the nested config
                    foreach (PropertyInfo nestedProperty in property.PropertyType.GetProperties())
                    {
                        ConfigAttribute? nestedConfigAttr = nestedProperty.GetCustomAttribute<ConfigAttribute>();
                        if (nestedConfigAttr == null || !nestedProperty.CanWrite)
                            continue;

                        try
                        {
                            // Get the value using the group name from ConfigGroupAttribute
                            Type propertyType = nestedProperty.PropertyType;
                            dynamic? value = GetLocalValue(propertyType, nestedConfigAttr.Name, configGroupAttr.Name);

                            if (value != null)
                            {
                                // Update the nested property
                                nestedProperty.SetValue(nestedConfigInstance, value);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to update nested config property {PropertyName} in group {GroupName}", nestedConfigAttr.Name, configGroupAttr.Name);
                        }
                    }
                    continue;
                }

                ConfigAttribute? configAttr = property.GetCustomAttribute<ConfigAttribute>();
                if (configAttr == null || !property.CanWrite)
                    continue;

                try
                {
                    // Get the value using reflection to call the generic GetLocalValue method
                    Type propertyType = property.PropertyType;

                    // Use dynamic for simplified reflection
                    dynamic? value = GetLocalValue(propertyType, configAttr.Name, configAttr.GroupName);

                    if (value != null)
                    {
                        // Try to validate the value if a validation method is specified
                        bool isValid = true;
                        ConfigValidationAttribute? validationAttr = property.GetCustomAttribute<ConfigValidationAttribute>();

                        if (validationAttr != null)
                        {
                            // Look for the validation method on the config instance
                            var validationMethod = configType.GetMethod(
                                validationAttr.ValidationMethodName,
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                null,
                                [propertyType],
                                null
                            );

                            if (validationMethod != null)
                            {
                                // Call the validation method
                                isValid = (bool)validationMethod.Invoke(configInstance, new[] { value })!;

                                // If validation fails, use the default value
                                if (!isValid)
                                {
                                    _logger.LogWarning($"Validation failed for config '{configAttr.Name}'. Using default value.");
                                    value = property.GetValue(configInstance);

                                    // Update the config value with the default value
                                    SetLocalValue(configAttr.Name, configAttr.GroupName, value);
                                }
                            }
                        }

                        // Update the property value
                        if (isValid)
                        {
                            property.SetValue(configInstance, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating config property '{property.Name}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Helper method to call GetLocalValue with the correct generic type
        /// </summary>
        private object? GetLocalValue(Type valueType, string configName, string groupName)
        {
            // Get the generic method
            var methodInfo = typeof(ConfigHandler).GetMethod(nameof(GetLocalValue), BindingFlags.NonPublic | BindingFlags.Instance, [typeof(string), typeof(string)]);

            // Make it generic with the target type
            var genericMethod = methodInfo?.MakeGenericMethod(valueType);

            // Call the method
            return genericMethod?.Invoke(this, [configName, groupName]);
        }

        /// <summary>
        /// Register a configuration value in a specific group
        ///
        /// Note: This only registers the configuration in memory.
        /// To load values from disk or save to disk, call LoadGroupConfig() or SaveGroupConfig() separately.
        /// </summary>
        /// <param name="groupName">The group name for this config (e.g. "general", "database")</param>
        /// <param name="configName">The unique name for this config setting</param>
        /// <param name="description">Human-readable description for the config</param>
        /// <param name="defaultValue">Default value to use if not specified in file</param>
        /// <param name="flags">Optional flags for this config (Global, Protected, Locked)</param>
        public void Register<T>(string groupName, string configName, string description, T defaultValue, ConfigFlag flags = ConfigFlag.None) where T : notnull
        {
            // Validate input parameters
            StringEx.ValidateName(configName, nameof(configName));
            StringEx.ValidateName(groupName, nameof(groupName));

            // Get or create module config
            var moduleConfig = _moduleConfigs.GetOrAdd(_plugin, _ => new ModuleConfig { ModuleName = _plugin.ModuleName });

            // Get or create the group
            var group = moduleConfig.Groups.GetOrAdd(groupName, _ => new ConfigGroup { Name = groupName });

            // Check for name conflicts in this group
            if (group.Items.ContainsKey(configName))
            {
                _logger.LogWarning($"Configuration '{configName}' already exists in group '{groupName}' for module '{_plugin.ModuleName}'");
                return;
            }

            // Create config item
            var configItem = new ConfigItem
            {
                Name = configName,
                Description = description,
                DefaultValue = defaultValue,
                Value = defaultValue,
                Flags = flags,
                GroupName = groupName
            };

            // Add item to group
            group.Items[configName] = configItem;

            // If this is a global config, register with the global registry
            if (flags.HasFlag(ConfigFlag.Global))
            {
                GlobalConfigRegistry.Instance.RegisterGlobalConfig(_plugin, configItem, groupName);
            }
        }

        /// <summary>
        /// Updates all config instances with a new value
        /// </summary>
        internal void UpdateConfigInstancesWithNewValue(string configName, object value)
        {
            // Find all config instances for this plugin
            foreach (var ((plugin, configType), configInstance) in _configInstances)
            {
                if (plugin != _plugin)
                    continue;

                // Find all properties with this config name
                foreach (var property in configType.GetProperties())
                {
                    ConfigAttribute? configAttr = property.GetCustomAttribute<ConfigAttribute>();
                    if (configAttr == null || configAttr.Name != configName || !property.CanWrite)
                        continue;

                    try
                    {
                        // Try to convert and set the value
                        var convertedValue = TypeConverter.ConvertDynamic(value, property.PropertyType);
                        if (convertedValue != null)
                        {
                            property.SetValue(configInstance, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to update property '{property.Name}' with value: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the config instance for a plugin
        /// </summary>
        public TConfig GetConfig<TConfig>() where TConfig : class, new()
        {
            var key = (_plugin, typeof(TConfig));

            // Try to get the existing instance
            if (_configInstances.TryGetValue(key, out var instance) && instance is TConfig typedConfig)
                return typedConfig;

            // Create a new instance if none exists
            var config = new TConfig();
            _configInstances[key] = config;

            // Register the config properties (async operation, but we'll continue without waiting)
            _ = RegisterConfigPropertiesAsync(config, typeof(TConfig));

            return config;
        }

        /// <summary>
        /// Creates a new config adapter for a specific config type
        /// </summary>
        internal ConfigAdapter<TConfig> GetConfigAdapter<TConfig>() where TConfig : class, new()
        {
            var config = GetConfig<TConfig>();
            return new ConfigAdapter<TConfig>(this, config);
        }

        #endregion

        #region Registration Methods

        /// <summary>
        /// Register standard database configuration values
        /// </summary>
        public void RegisterDatabaseConfigs()
        {
            // Register all database configs in memory
            Register("database", "host", "The hostname or IP address of the MySQL server (e.g., localhost or 192.168.1.100)", "localhost", ConfigFlag.Locked);
            Register("database", "port", "The port number used to connect to the MySQL server", 3306, ConfigFlag.Locked);
            Register("database", "username", "The username used to authenticate with the MySQL database", "example-username", ConfigFlag.Locked);
            Register("database", "password", "The password corresponding to the MySQL username", "example-password", ConfigFlag.Locked);
            Register("database", "database", "The name of the specific MySQL database to connect to", "example-database", ConfigFlag.Locked);
            Register("database", "ssl-mode", "Specifies how SSL should be used when connecting to the database. Options: none, preferred, required, verifyca, verifyfull", "preferred", ConfigFlag.Locked);
        }

        /// <summary>
        /// Creates a new MySqlConnection with properly configured connection string
        /// </summary>
        public string GetConnectionString()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConfigHandler),
                    $"Cannot create database connection for disposed plugin {_configOwner}");
            }

            var host = GetLocalValue<string>("host", "database");
            var port = GetLocalValue<int>("port", "database");
            var database = GetLocalValue<string>("database", "database");
            var username = GetLocalValue<string>("username", "database");
            var password = GetLocalValue<string>("password", "database");
            var sslMode = GetLocalValue<string>("ssl-mode", "database") ?? "preferred";

            var connectionStringBuilder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = (uint)port,
                Database = database,
                UserID = username,
                Password = password,
                CharacterSet = "utf8mb4",
                ConnectionTimeout = 30,
                SslMode = Enum.TryParse<MySqlSslMode>(sslMode, true, out var parsedSslMode) ? parsedSslMode : MySqlSslMode.Preferred
            };

            return new MySqlConnection(connectionStringBuilder.ConnectionString).ConnectionString;
        }

        #endregion

        #region Value Access Methods

        /// <summary>
        /// Get a local configuration value
        /// </summary>
        internal T? GetLocalValue<T>(string configName, string groupName)
        {
            if (!_moduleConfigs.TryGetValue(_plugin, out var moduleConfig))
                return default;

            if (!moduleConfig.Groups.TryGetValue(groupName, out var group))
                return default;

            if (!group.Items.TryGetValue(configName, out var configItem))
                return default;

            // Value is null, return default
            if (configItem.Value == null)
                return default;

            // Direct match with requested type
            if (configItem.Value is T typedValue)
                return typedValue;

            // Try conversion using TypeConverter
            try
            {
                var converted = TypeConverter.Convert<T>(configItem.Value);
                if (converted != null)
                {
                    // Update cache with converted value (for efficiency in future calls)
                    configItem.Value = converted;
                    return converted;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Value conversion failed for key '{configName}': {ex.Message}");
            }

            // Fall back to default value if possible
            if (configItem.DefaultValue is T defaultValue)
                return defaultValue;

            return default;
        }

        /// <summary>
        /// Set a local configuration value
        /// </summary>
        internal bool SetLocalValue<T>(string configName, string groupName, T value) where T : notnull
        {
            if (!_moduleConfigs.TryGetValue(_plugin, out var moduleConfig))
                return false;

            if (!moduleConfig.Groups.TryGetValue(groupName, out var group))
                return false;

            if (!group.Items.TryGetValue(configName, out var configItem))
                return false;

            // Check if locked
            if (configItem.Flags.HasFlag(ConfigFlag.Locked))
            {
                _logger.LogWarning($"Config '{configName}' is locked and cannot be modified at runtime");
                return false;
            }

            // Update value
            configItem.Value = value;

            // Update any config instances
            UpdateConfigInstancesWithNewValue(configName, value!);

            // Global configs do not need special handling here

            return true;
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            // Remove from module configs
            _moduleConfigs.TryRemove(_plugin, out _);

            // Remove config instances
            foreach (var key in _configInstances.Keys.Where(k => k.Item1 == _plugin).ToList())
            {
                _configInstances.TryRemove(key, out _);
            }

            // Unregister from global config registry
            GlobalConfigRegistry.Instance.UnregisterPlugin(_plugin);

            _disposed = true;
        }

        #endregion
    }
}