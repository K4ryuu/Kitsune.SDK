using System.Runtime.CompilerServices;
using Kitsune.SDK.Services.Data.Base;
using Kitsune.SDK.Core.Base;

namespace Kitsune.SDK.Services.Data.Cache.Keys
{
    /// <summary>
    /// Optimized cache key structure to avoid string allocations
    /// </summary>
    internal readonly struct CacheKey : IEquatable<CacheKey>
    {
        public readonly ulong SteamId;
        public readonly PlayerDataHandler.DataType DataType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CacheKey(ulong steamId, PlayerDataHandler.DataType dataType)
        {
            SteamId = steamId;
            DataType = dataType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CacheKey other) => SteamId == other.SteamId && DataType == other.DataType;

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(SteamId, DataType);

        public override string ToString() => $"{SteamId}:{DataType}";
    }

    /// <summary>
    /// Instance cache key for typed storage/settings
    /// </summary>
    internal readonly struct InstanceCacheKey : IEquatable<InstanceCacheKey>
    {
        public readonly SdkPlugin Plugin;
        public readonly Type InstanceType;
        public readonly ulong SteamId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstanceCacheKey(SdkPlugin plugin, Type instanceType, ulong steamId)
        {
            Plugin = plugin;
            InstanceType = instanceType;
            SteamId = steamId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(InstanceCacheKey other) 
            => Plugin == other.Plugin && InstanceType == other.InstanceType && SteamId == other.SteamId;

        public override bool Equals(object? obj) => obj is InstanceCacheKey other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(Plugin.GetHashCode(), InstanceType.GetHashCode(), SteamId);
    }

    /// <summary>
    /// Dynamic instance cache key
    /// </summary>
    internal readonly struct DynamicCacheKey : IEquatable<DynamicCacheKey>
    {
        public readonly SdkPlugin Plugin;
        public readonly ulong SteamId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynamicCacheKey(SdkPlugin plugin, ulong steamId)
        {
            Plugin = plugin;
            SteamId = steamId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DynamicCacheKey other) => Plugin == other.Plugin && SteamId == other.SteamId;

        public override bool Equals(object? obj) => obj is DynamicCacheKey other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(Plugin.GetHashCode(), SteamId);
    }
}