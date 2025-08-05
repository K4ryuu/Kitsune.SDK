using System.Reflection;
using Kitsune.SDK.Core.Attributes.Config;

namespace Kitsune.SDK.Services.Config
{
    /// <summary>
    /// Provides a typed interface for accessing configuration values
    /// </summary>
    /// <typeparam name="TConfig">The configuration type</typeparam>
    public class ConfigAdapter<TConfig> where TConfig : class, new()
    {
        private readonly ConfigHandler _handler;
        private readonly TConfig _instance;
        private readonly Dictionary<string, string> _propertyToConfigMap = [];
        private readonly Dictionary<string, string> _propertyToGroupMap = [];

        internal ConfigAdapter(ConfigHandler handler, TConfig instance)
        {
            _handler = handler;
            _instance = instance;

            // Build the property mappings
            BuildPropertyMappings();
        }

        private void BuildPropertyMappings()
        {
            foreach (PropertyInfo property in typeof(TConfig).GetProperties())
            {
                ConfigAttribute? configAttr = property.GetCustomAttribute<ConfigAttribute>();
                if (configAttr == null)
                    continue;

                _propertyToConfigMap[property.Name] = configAttr.Name;
                _propertyToGroupMap[property.Name] = configAttr.GroupName;
            }
        }

        /// <summary>
        /// Gets a configuration value by property name
        /// </summary>
        public T? GetConfigValue<T>(string propertyName) where T : notnull
        {
            if (!_propertyToConfigMap.TryGetValue(propertyName, out var configName) || !_propertyToGroupMap.TryGetValue(propertyName, out var groupName))
                return default;

            return _handler.GetLocalValue<T>(configName, groupName);
        }

        /// <summary>
        /// Sets a configuration value by property name
        /// </summary>
        public bool SetConfigValue<T>(string propertyName, T value) where T : notnull
        {
            if (!_propertyToConfigMap.TryGetValue(propertyName, out var configName) || !_propertyToGroupMap.TryGetValue(propertyName, out var groupName))
                return false;

            return _handler.SetLocalValue(configName, groupName, value);
        }

        /// <summary>
        /// Gets the config instance
        /// </summary>
        public TConfig Instance => _instance;
    }
}