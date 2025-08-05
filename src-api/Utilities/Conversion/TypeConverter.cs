using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Kitsune.SDK.Utilities
{
    /// <summary>
    /// Simple, fast type converter optimized for game development
    /// </summary>
    public static class TypeConverter
    {
        // Cache of conversion functions
        private static readonly ConcurrentDictionary<(Type, Type), Func<object, object?>> _converters = new();

        // Static constructor to register common conversions
        static TypeConverter()
        {
            // String conversions
            RegisterConverter<string, int>(s => int.Parse(s, CultureInfo.InvariantCulture));
            RegisterConverter<string, long>(s => long.Parse(s, CultureInfo.InvariantCulture));
            RegisterConverter<string, float>(s => float.Parse(s, CultureInfo.InvariantCulture));
            RegisterConverter<string, double>(s => double.Parse(s, CultureInfo.InvariantCulture));
            RegisterConverter<string, bool>(bool.Parse);
            RegisterConverter<string, DateTime>(s => DateTime.Parse(s, CultureInfo.InvariantCulture));

            // Primitive types to string
            RegisterConverter<int, string>(i => i.ToString(CultureInfo.InvariantCulture));
            RegisterConverter<long, string>(l => l.ToString(CultureInfo.InvariantCulture));
            RegisterConverter<float, string>(f => f.ToString(CultureInfo.InvariantCulture));
            RegisterConverter<double, string>(d => d.ToString(CultureInfo.InvariantCulture));

            // JsonElement conversions
            RegisterConverter<JsonElement, string>(el => el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.ToString());
            RegisterConverter<JsonElement, int>(el => el.GetInt32());
            RegisterConverter<JsonElement, long>(el => el.GetInt64());
            RegisterConverter<JsonElement, bool>(el => el.GetBoolean());
            RegisterConverter<JsonElement, double>(el => el.GetDouble());
            RegisterConverter<JsonElement, float>(el => (float)el.GetDouble());
            RegisterConverter<JsonElement, Dictionary<string, object?>>(el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText()) ?? []);
            RegisterConverter<JsonElement, List<string>>(el => el.ValueKind == JsonValueKind.Array ? [.. el.EnumerateArray().Select(e => e.GetString() ?? e.ToString())] : []);
            RegisterConverter<JsonElement, List<int>>(el => el.ValueKind == JsonValueKind.Array ? [.. el.EnumerateArray().Select(e => e.GetInt32())] : []);

            // Collection conversions
            RegisterConverter<List<object>, List<string>>(list => [.. list.Select(x => x?.ToString() ?? string.Empty)]);
            RegisterConverter<List<object>, List<int>>(list => [.. list.Select(x => System.Convert.ToInt32(x))]);
            RegisterConverter<List<object>, List<double>>(list => [.. list.Select(x => System.Convert.ToDouble(x))]);
            RegisterConverter<List<object>, List<float>>(list => [.. list.Select(x => System.Convert.ToSingle(x))]);
        }

        /// <summary>
        /// Register a custom type converter
        /// </summary>
        public static void RegisterConverter<TSource, TTarget>(Func<TSource, TTarget> converter)
        {
            _converters[(typeof(TSource), typeof(TTarget))] = obj => converter((TSource)obj)!;
        }

        /// <summary>
        /// Convert a value to the specified type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Convert<T>(object? value)
        {
            // Fast exit for null values
            if (value == null)
                return default;

            // Fast path - already the correct type
            if (value is T typedValue)
                return typedValue;

            Type targetType = typeof(T);
            Type sourceType = value.GetType();

            try
            {
                // Use registered converter if available
                if (_converters.TryGetValue((sourceType, targetType), out var converter))
                {
                    return (T?)converter(value);
                }

                // Special handling for JsonElement
                if (value is JsonElement element)
                {
                    // String conversion
                    if (targetType == typeof(string))
                    {
                        return (T)(object)(element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString());
                    }

                    // Direct conversion for simple types
                    if (targetType == typeof(int) && element.ValueKind == JsonValueKind.Number)
                    {
                        return (T)(object)element.GetInt32();
                    }

                    if (targetType == typeof(double) && element.ValueKind == JsonValueKind.Number)
                    {
                        return (T)(object)element.GetDouble();
                    }

                    if (targetType == typeof(bool))
                    {
                        return (T)(object)(element.ValueKind == JsonValueKind.True || (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out bool result) && result));
                    }

                    // Collection handling
                    return JsonSerializer.Deserialize<T>(element.GetRawText());
                }

                // Special handling for string - ToString() is faster
                if (targetType == typeof(string))
                {
                    return (T)(object)(value.ToString() ?? string.Empty);
                }

                // System.Convert for primitive types
                if (targetType.IsPrimitive || targetType == typeof(decimal) || targetType == typeof(DateTime))
                {
                    return (T)System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                }

                // Last resort: JSON serialization round-trip
                string json = JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Try to convert a value to the specified type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvert<T>(object? value, out T? result)
        {
            result = Convert<T>(value);
            return result != null || (value == null && default(T) == null);
        }

        /// <summary>
        /// Dynamically convert a value to the specified type
        /// </summary>
        public static object? ConvertDynamic(object? value, Type targetType)
        {
            if (value == null || targetType == null)
                return null;

            // Fast path - already the correct type
            if (targetType.IsInstanceOfType(value))
                return value;

            Type sourceType = value.GetType();

            try
            {
                // Use registered converter if available
                if (_converters.TryGetValue((sourceType, targetType), out var converter))
                {
                    return converter(value);
                }

                // Special handling for JsonElement
                if (value is JsonElement element)
                {
                    if (targetType == typeof(string))
                    {
                        return element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString();
                    }

                    if (targetType == typeof(int) && element.ValueKind == JsonValueKind.Number)
                    {
                        return element.GetInt32();
                    }

                    if (targetType == typeof(long) && element.ValueKind == JsonValueKind.Number)
                    {
                        return element.GetInt64();
                    }

                    if (targetType == typeof(bool))
                    {
                        return element.ValueKind == JsonValueKind.True;
                    }

                    // Other types
                    return JsonSerializer.Deserialize(element.GetRawText(), targetType);
                }

                // Special handling for string
                if (targetType == typeof(string))
                {
                    return value.ToString();
                }

                // Primitive type conversion
                if (targetType.IsPrimitive || targetType == typeof(decimal) || targetType == typeof(DateTime))
                {
                    return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                }

                // Last resort: JSON serialization
                string json = JsonSerializer.Serialize(value);
                return JsonSerializer.Deserialize(json, targetType);
            }
            catch
            {
                return null;
            }
        }
    }
}