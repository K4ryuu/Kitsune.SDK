using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Kitsune.SDK.Core.Attributes.Data;

namespace Kitsune.SDK.Services
{
    /// <summary>
    /// Caches reflection metadata to avoid repeated reflection calls
    /// </summary>
    internal static class MetadataCache
    {
        /// <summary>
        /// Property metadata for a type
        /// </summary>
        internal sealed class PropertyMetadata
        {
            public readonly PropertyInfo Property;
            public readonly string Name;
            public readonly object? DefaultValue;
            public readonly bool IsTracked;
            public readonly bool IsAutoProperty;

            public PropertyMetadata(PropertyInfo property, string name, object? defaultValue, bool isTracked)
            {
                Property = property;
                Name = name;
                DefaultValue = defaultValue;
                IsTracked = isTracked;

                // Check if it's an auto-property (has compiler-generated backing field)
                IsAutoProperty = property.DeclaringType != typeof(StorageBase) &&
                               property.DeclaringType != typeof(SettingsBase);
            }
        }

        /// <summary>
        /// Type metadata containing all properties with attributes
        /// </summary>
        internal sealed class TypeMetadata
        {
            public readonly Type Type;
            public readonly PropertyMetadata[] StorageProperties;
            public readonly PropertyMetadata[] SettingsProperties;
            public readonly Dictionary<string, PropertyMetadata> StorageByName;
            public readonly Dictionary<string, PropertyMetadata> SettingsByName;
            public readonly HashSet<string> TrackedProperties;

            public TypeMetadata(Type type)
            {
                Type = type;
                var storageList = new List<PropertyMetadata>();
                var settingsList = new List<PropertyMetadata>();
                TrackedProperties = new HashSet<string>(StringComparer.Ordinal);

                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Check for StorageAttribute
                    var storageAttr = property.GetCustomAttribute<StorageAttribute>();
                    if (storageAttr != null)
                    {
                        var metadata = new PropertyMetadata(
                            property,
                            storageAttr.Name,
                            GetDefaultValue(property),
                            storageAttr.Track
                        );
                        storageList.Add(metadata);
                        if (storageAttr.Track)
                        {
                            TrackedProperties.Add(storageAttr.Name);
                        }
                    }

                    // Check for SettingAttribute
                    var settingAttr = property.GetCustomAttribute<SettingAttribute>();
                    if (settingAttr != null)
                    {
                        var metadata = new PropertyMetadata(
                            property,
                            settingAttr.Name,
                            GetDefaultValue(property),
                            false // Settings are not tracked
                        );
                        settingsList.Add(metadata);
                    }
                }

                StorageProperties = storageList.ToArray();
                SettingsProperties = settingsList.ToArray();

                // Create lookup dictionaries
                StorageByName = StorageProperties.ToDictionary(p => p.Name, StringComparer.Ordinal);
                SettingsByName = SettingsProperties.ToDictionary(p => p.Name, StringComparer.Ordinal);
            }

            private static object? GetDefaultValue(PropertyInfo property)
            {
                try
                {
                    // For value types, use Activator to get default
                    if (property.PropertyType.IsValueType)
                    {
                        return Activator.CreateInstance(property.PropertyType);
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        // Cache of type metadata
        private static readonly ConcurrentDictionary<Type, TypeMetadata> _typeCache = new();

        /// <summary>
        /// Get or create metadata for a type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypeMetadata GetTypeMetadata(Type type)
        {
            return _typeCache.GetOrAdd(type, static t => new TypeMetadata(t));
        }

        /// <summary>
        /// Clear the cache (useful for testing)
        /// </summary>
        internal static void Clear()
        {
            _typeCache.Clear();
        }
    }
}