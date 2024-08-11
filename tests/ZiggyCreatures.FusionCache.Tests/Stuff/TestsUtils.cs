﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;
using ZiggyCreatures.Caching.Fusion.Serialization.NeueccMessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;
using ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests.Stuff;

public static class TestsUtils
{
	public static IFusionCacheSerializer GetSerializer(SerializerType serializerType)
	{
		switch (serializerType)
		{
			case SerializerType.NewtonsoftJson:
				return new FusionCacheNewtonsoftJsonSerializer();
			case SerializerType.SystemTextJson:
				return new FusionCacheSystemTextJsonSerializer();
			case SerializerType.ServiceStackJson:
				return new FusionCacheServiceStackJsonSerializer();
			case SerializerType.NeueccMessagePack:
				return new FusionCacheNeueccMessagePackSerializer();
			case SerializerType.ProtoBufNet:
				return new FusionCacheProtoBufNetSerializer();
			case SerializerType.CysharpMemoryPack:
				return new FusionCacheCysharpMemoryPackSerializer();
			default:
				throw new ArgumentException("Invalid serializer specified", nameof(serializerType));
		}
	}

	public static string MaybePreProcessCacheKey(string key, string? prefix)
	{
		if (prefix is null)
			return key;

		return prefix + key;
	}

	public static TimeSpan PlusALittleBit(this TimeSpan ts)
	{
		return ts + TimeSpan.FromMilliseconds(250);
	}

	public static TimeSpan PlusASecond(this TimeSpan ts)
	{
		return ts + TimeSpan.FromSeconds(1);
	}

	public static FusionCacheEntryOptions SetFactoryTimeoutsMs(this FusionCacheEntryOptions options, int? softTimeoutMs = null, int? hardTimeoutMs = null, bool? keepTimedOutFactoryResult = null)
	{
		if (softTimeoutMs is not null)
			options.FactorySoftTimeout = TimeSpan.FromMilliseconds(softTimeoutMs.Value);
		if (hardTimeoutMs is not null)
			options.FactoryHardTimeout = TimeSpan.FromMilliseconds(hardTimeoutMs.Value);
		if (keepTimedOutFactoryResult is not null)
			options.AllowTimedOutFactoryBackgroundCompletion = keepTimedOutFactoryResult.Value;
		return options;
	}

	public static ILogger? GetLogger(IFusionCache cache)
	{
		return typeof(FusionCache).GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as ILogger;
	}

	public static IFusionCacheMemoryLocker? GetMemoryLocker(IFusionCache cache)
	{
		return typeof(FusionCache).GetField("_memoryLocker", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as IFusionCacheMemoryLocker;
	}

	public static IMemoryCache? GetMemoryCache(IFusionCache cache)
	{
		var _mca = typeof(FusionCache).GetField("_mca", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache);
		return _mca!.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_mca) as IMemoryCache;
	}

	public static IFusionCacheSerializer? GetSerializer(IFusionCache cache)
	{
		var dca = typeof(FusionCache).GetField("_dca", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as DistributedCacheAccessor;
		if (dca is null)
			return null;

		return typeof(DistributedCacheAccessor).GetField("_serializer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dca) as IFusionCacheSerializer;
	}

	public static IDistributedCache? GetDistributedCache<TDistributedCache>(IFusionCache cache)
		where TDistributedCache : class, IDistributedCache
	{
		var dca = typeof(FusionCache).GetField("_dca", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as DistributedCacheAccessor;
		if (dca is null)
			return null;

		return typeof(DistributedCacheAccessor).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dca) as TDistributedCache;
	}

	public static TBackplane? GetBackplane<TBackplane>(IFusionCache cache)
		where TBackplane : class, IFusionCacheBackplane
	{
		var bpa = typeof(FusionCache).GetField("_bpa", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as BackplaneAccessor;
		if (bpa is null)
			return null;

		return typeof(BackplaneAccessor).GetField("_backplane", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bpa) as TBackplane;
	}

	public static RedisBackplaneOptions? GetRedisBackplaneOptions(IFusionCache cache)
	{
		var backplane = GetBackplane<RedisBackplane>(cache);
		if (backplane is null)
			return null;

		return typeof(RedisBackplane).GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(backplane) as RedisBackplaneOptions; ;
	}

	public static IFusionCachePlugin[]? GetPlugins(IFusionCache cache)
	{
		return (typeof(FusionCache).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache) as List<IFusionCachePlugin>)?.ToArray();
	}
}
