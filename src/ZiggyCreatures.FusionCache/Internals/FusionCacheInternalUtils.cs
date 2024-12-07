using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals.Backplane;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion.Internals;

internal static class FusionCacheInternalUtils
{
	internal static class GeneratorUtils
	{
		private static readonly char[] _chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();
		private static long _lastId = DateTime.UtcNow.Ticks;
		private static readonly ThreadLocal<char[]> _buffer = new ThreadLocal<char[]>(() => new char[13]);

		private static string GenerateOperationId(long id)
		{
			// SEE: https://nimaara.com/2018/10/10/generating-ids-in-csharp.html

			char[] buffer = _buffer.Value!;

			buffer[0] = _chars[(int)(id >> 60) & 31];
			buffer[1] = _chars[(int)(id >> 55) & 31];
			buffer[2] = _chars[(int)(id >> 50) & 31];
			buffer[3] = _chars[(int)(id >> 45) & 31];
			buffer[4] = _chars[(int)(id >> 40) & 31];
			buffer[5] = _chars[(int)(id >> 35) & 31];
			buffer[6] = _chars[(int)(id >> 30) & 31];
			buffer[7] = _chars[(int)(id >> 25) & 31];
			buffer[8] = _chars[(int)(id >> 20) & 31];
			buffer[9] = _chars[(int)(id >> 15) & 31];
			buffer[10] = _chars[(int)(id >> 10) & 31];
			buffer[11] = _chars[(int)(id >> 5) & 31];
			buffer[12] = _chars[(int)id & 31];

			return new string(buffer, 0, buffer.Length);
		}

		public static string GenerateOperationId()
		{
			return GenerateOperationId(Interlocked.Increment(ref _lastId));
		}
	}

	private static readonly DateTimeOffset DateTimeOffsetMaxValue = DateTimeOffset.MaxValue;
	private const string DateTimeOffsetFormatString = "yyyy-MM-ddTHH:mm:ss.ffffff";
	private static readonly TimeSpan TimeSpanMaxValue = TimeSpan.MaxValue;
	private static readonly Type CacheItemPriorityType = typeof(CacheItemPriority);
	public static readonly string[]? NoTags = null;

	public static T[]? AsArray<T>(this IEnumerable<T>? items)
	{
		return items switch
		{
			null => null,
			T[] => (T[])items,
			_ => items.ToArray()
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long GetCurrentTimestamp()
	{
		return DateTimeOffset.UtcNow.UtcTicks;
	}

	public static string MaybeGenerateOperationId(ILogger? logger)
	{
		if (logger is null)
			return string.Empty;

		return GeneratorUtils.GenerateOperationId();
	}

	public static string GenerateOperationId()
	{
		return GeneratorUtils.GenerateOperationId();
	}

	// SEE HERE: https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
	public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
		where TKey : notnull
	{
		if (dictionary is null)
			throw new ArgumentNullException(nameof(dictionary));

		return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, value));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsLogicallyExpired(this IFusionCacheEntry? entry)
	{
		if (entry?.Metadata is null)
			return false;

		return entry.Metadata.IsLogicallyExpired();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsStale(this IFusionCacheEntry entry)
	{
		return entry.Metadata?.IsFromFailSafe ?? false;
	}

	public static Exception GetSingleInnerExceptionOrSelf(this AggregateException exc)
	{
		return (exc.InnerException is not null && exc.InnerExceptions?.Count <= 1)
			? exc.InnerException
			: exc
		;
	}

	public static string? ToLogString_Expiration(this DateTimeOffset dt)
	{
		var now = DateTimeOffset.UtcNow;
		var delta = dt - now;

		if (delta == TimeSpan.Zero)
			return "now";

		if (delta.TotalSeconds > -1 && delta.TotalSeconds < 1)
			return delta.TotalMilliseconds.ToString("0") + "ms";

		if (delta.TotalMinutes > -1 && delta.TotalMinutes < 1)
			return delta.TotalSeconds.ToString("0") + "s";

		if (delta.TotalHours > -1 && delta.TotalHours < 1)
			return delta.TotalMinutes.ToString("0") + "m";

		return dt.ToString(DateTimeOffsetFormatString);
	}

	public static string? ToLogString_Expiration(this DateTimeOffset? dt)
	{
		if (dt.HasValue == false)
			return "/";

		return dt.Value.ToLogString_Expiration();
	}

	public static string? ToLogString(this DateTimeOffset dt)
	{
		return dt.ToString(DateTimeOffsetFormatString);
	}

	public static string? ToLogString(this DateTimeOffset? dt)
	{
		if (dt is null)
			return "/";

		return dt.Value.ToString(DateTimeOffsetFormatString);
	}

	public static string? ToLogString(this MemoryCacheEntryOptions? options)
	{
		if (options is null)
			return null;

		return $"MEO[CEXP={options.AbsoluteExpiration!.Value.ToLogString_Expiration()}, PR={options.Priority.ToLogString()}, S={options.Size?.ToString()}]";
	}

	public static string? ToLogString(this DistributedCacheEntryOptions? options)
	{
		if (options is null)
			return null;

		return $"DEO[CEXP={options.AbsoluteExpiration?.ToLogString_Expiration()}]";
	}

	public static string? ToLogString(this FusionCacheEntryOptions? options)
	{
		if (options is null)
			return null;

		return "FEO" + options.ToString();
	}

	public static string? ToLogString(this IFusionCacheEntry? entry, bool includeTags)
	{
		if (entry is null)
			return null;

		return $"FE({(entry is IFusionCacheMemoryEntry ? "M" : "D")})@{new DateTimeOffset(entry.Timestamp, TimeSpan.Zero).ToLogString()}{(includeTags ? GetTagsLogString(entry.Tags) : null)}{entry.Metadata?.ToString() ?? "[]"}";

		static string GetTagsLogString(string[]? tags)
		{
			if (tags is null || tags.Length == 0)
			{
				return "[T=]";
			}

			return $"[T={string.Join(",", tags)}]";
		}
	}

	public static string? ToLogString(this TimeSpan ts)
	{
		if (ts == TimeSpan.Zero)
			return "0";

		if (ts.TotalSeconds > -1 && ts.TotalSeconds < 1)
			return ts.TotalMilliseconds.ToString("0") + "ms";

		if (ts.TotalMinutes > -1 && ts.TotalMinutes < 1)
			return ts.TotalSeconds.ToString("0") + "s";

		if (ts.TotalHours > -1 && ts.TotalHours < 1)
			return ts.TotalMinutes.ToString("0") + "m";

		return ts.ToString();
	}

	public static string? ToLogString(this TimeSpan? ts)
	{
		if (ts.HasValue == false)
			return "/";

		return ts.Value.ToLogString();
	}

	public static string? ToLogString_Timeout(this TimeSpan ts)
	{
		if (ts == Timeout.InfiniteTimeSpan)
			return "/";

		return ts.ToLogString();
	}

	public static string ToLogString(this CacheItemPriority priority)
	{
		return priority switch
		{
			CacheItemPriority.Low => "L",
			CacheItemPriority.Normal => "N",
			CacheItemPriority.High => "H",
			CacheItemPriority.NeverRemove => "NR",
			// FALLBACK
			_ => Enum.GetName(CacheItemPriorityType, priority) ?? "",
		};
	}

	public static string ToLogString(this long? value)
	{
		if (value.HasValue == false)
			return "/";

		return value.Value.ToString();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ToStringYN(this bool b)
	{
		return b ? "Y" : "N";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string? ToString(this bool b, string? trueString, string? falseString = null)
	{
		return b ? trueString : falseString;
	}

	public static FusionCacheDistributedEntry<TValue> AsDistributedEntry<TValue>(this IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (entry is FusionCacheDistributedEntry<TValue> entry1)
			return entry1;

		// TODO: CHECK THIS AGAIN
		return FusionCacheDistributedEntry<TValue>.CreateFromOptions(entry.GetValue<TValue>(), entry.Tags, options, entry.IsStale(), entry.Metadata?.LastModified, entry.Metadata?.ETag, entry.Timestamp);
		//return FusionCacheDistributedEntry<TValue>.CreateFromOtherEntry(entry, options);
	}

	public static FusionCacheMemoryEntry<TValue> AsMemoryEntry<TValue>(this IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (entry is FusionCacheMemoryEntry<TValue> entry1)
			return entry1;

		return FusionCacheMemoryEntry<TValue>.CreateFromOtherEntry(entry, options);
	}

	public static void SafeExecute<TEventArgs>(this EventHandler<TEventArgs> ev, string? operationId, string? key, IFusionCache cache, TEventArgs eventArgs, string eventName, ILogger? logger, LogLevel logLevel, bool syncExecution)
	{
		static void ExecuteInvocations(string? operationId, string? key, IFusionCache cache, string eventName, TEventArgs e, Delegate[] invocations, ILogger? logger, LogLevel errorLogLevel)
		{
			foreach (EventHandler<TEventArgs> invocation in invocations.Cast<EventHandler<TEventArgs>>())
			{
				try
				{
					invocation(cache, e);
				}
				catch (Exception exc)
				{
					logger?.Log(errorLogLevel, exc, "FUSION [N={CacheName} I={CacheInstanceId}] (O={CacheOperationId} K={CacheKey}): an error occurred while handling an event handler for {EventName}", cache.CacheName, cache.InstanceId, operationId, key, eventName);
				}
			}
		}

		var invocations = ev.GetInvocationList();

		if (invocations is null || invocations.Length == 0)
			return;

		// WE ONLY TEST IF THE LOG LEVEL IS ENABLED ONCE: IN THAT CASE WE'LL USE THE LOGGER, OTHERWISE WE SET IT TO null TO AVOID CHECKING IT EVERY TIME INSIDE THE LOOP
		if (logger is not null && logger.IsEnabled(logLevel) == false)
			logger = null;

		if (syncExecution)
		{
			ExecuteInvocations(operationId, key, cache, eventName, eventArgs, invocations, logger, logLevel);
		}
		else
		{
			Task.Run(() => ExecuteInvocations(operationId, key, cache, eventName, eventArgs, invocations, logger, logLevel));
		}
	}

	public static string GetBackplaneChannelName(this FusionCacheOptions options)
	{
		var prefix = options.BackplaneChannelPrefix;
		if (string.IsNullOrWhiteSpace(prefix))
			prefix = options.CacheName;

		// SAFETY NET (BUT IT SHOULD NOT HAPPEN)
		if (string.IsNullOrWhiteSpace(prefix))
			prefix = FusionCacheOptions.DefaultCacheName;

		return $"{prefix}.Backplane{FusionCacheOptions.BackplaneWireFormatSeparator}{FusionCacheOptions.BackplaneWireFormatVersion}";
	}

	public static DateTimeOffset GetNormalizedAbsoluteExpiration(TimeSpan duration, FusionCacheEntryOptions options, bool allowJittering)
	{
		// EARLY RETURN: COMMON CASE FOR WHEN USERS DO NOT WANT EXPIRATION
		if (duration == TimeSpanMaxValue)
			return DateTimeOffsetMaxValue;

		if (allowJittering && options.JitterMaxDuration > TimeSpan.Zero)
		{
			// EARLY RETURN: WHEN THE VALUES ARE NOT THE LIMITS BUT ARE STRETCHED VERY NEAR THEM
			if (duration > (TimeSpanMaxValue - options.JitterMaxDuration))
				return DateTimeOffsetMaxValue;

			// ADD JITTERING
			duration += TimeSpan.FromMilliseconds(options.GetJitterDurationMs());
		}

		// EARLY RETURN: WHEN OVERFLOWING DateTimeOffset.MaxValue
		var now = DateTimeOffset.UtcNow;
		if (duration > (DateTimeOffsetMaxValue - now))
			return DateTimeOffsetMaxValue;

		return now.Add(duration);
	}

	public static DateTimeOffset? GetNormalizedEagerExpiration(bool isFromFailSafe, float? eagerRefreshThreshold, DateTimeOffset normalizedExpiration)
	{
		if (isFromFailSafe)
			return null;

		if (eagerRefreshThreshold.HasValue == false)
			return null;

		var now = DateTimeOffset.UtcNow;

		return now.AddTicks((long)((normalizedExpiration - now).Ticks * eagerRefreshThreshold.Value));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool ShouldRead(this MemoryCacheAccessor mca, FusionCacheEntryOptions options)
	{
		if (options.SkipMemoryCacheRead)
			return false;

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool ShouldWrite(this MemoryCacheAccessor mca, FusionCacheEntryOptions options)
	{
		if (options.SkipMemoryCacheWrite)
			return false;

		return true;
	}

	public static bool ShouldRead(this DistributedCacheAccessor? dca, FusionCacheEntryOptions options)
	{
		if (dca is null)
			return false;

		if (options.SkipDistributedCacheRead)
			return false;

		return true;
	}

	public static bool ShouldReadWhenStale(this DistributedCacheAccessor? dca, FusionCacheEntryOptions options)
	{
		if (dca is null)
			return false;

		if (options.SkipDistributedCacheRead || options.SkipDistributedCacheReadWhenStale)
			return false;

		return true;
	}

	public static bool ShouldWrite(this DistributedCacheAccessor? dca, FusionCacheEntryOptions options)
	{
		if (dca is null)
			return false;

		if (options.SkipDistributedCacheWrite)
			return false;

		return true;
	}

	public static bool CanBeUsed(this DistributedCacheAccessor? dca, string? operationId, string? key)
	{
		if (dca is null)
			return false;

		if (dca.IsCurrentlyUsable(operationId, key) == false)
			return false;

		return true;
	}

	public static bool ShouldWrite(this BackplaneAccessor? bpa, FusionCacheEntryOptions options)
	{
		if (bpa is null)
			return false;

		if (options.SkipBackplaneNotifications)
			return false;

		return true;
	}

	public static bool CanBeUsed(this BackplaneAccessor? bpa, string? operationId, string? key)
	{
		if (bpa is null)
			return false;

		if (bpa.IsCurrentlyUsable(operationId, key) == false)
			return false;

		return true;
	}

	public static Task<long> SharedTagExpirationDataFactoryAsync(FusionCacheFactoryExecutionContext<long> ctx, CancellationToken token)
	{
		if (ctx.HasStaleValue)
			return Task.FromResult(ctx.StaleValue.Value);

		return Task.FromResult(0L);
	}

	public static long SharedTagExpirationDataFactory(FusionCacheFactoryExecutionContext<long> ctx, CancellationToken token)
	{
		if (ctx.HasStaleValue)
			return ctx.StaleValue.Value;

		return 0L;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool RequiresMetadata(FusionCacheEntryOptions options, FusionCacheEntryMetadata? metadata)
	{
		return
			metadata is not null
			|| options.IsFailSafeEnabled
			|| options.EagerRefreshThreshold is not null
			|| options.Size is not null
		;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool RequiresMetadata(FusionCacheEntryOptions options, bool isFromFailSafe, DateTimeOffset? lastModified, string? etag)
	{
		return
			options.IsFailSafeEnabled
			|| options.EagerRefreshThreshold.HasValue
			|| options.Size is not null
			|| isFromFailSafe
			|| lastModified is not null
			|| etag is not null
		;
	}

	// SOURCE: https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.Caching.Hybrid/Internal/ImmutableTypeCache.cs
	// COPIED AS-IS FOR MAXIMUM COMPATIBILITY WITH HybridCache
	public static bool IsTypeImmutable(Type type)
	{
		// check for known types
		if (type == typeof(string))
		{
			return true;
		}

		if (type.IsValueType)
		{
			// switch from Foo? to Foo if necessary
			if (Nullable.GetUnderlyingType(type) is { } nullable)
			{
				type = nullable;
			}
		}

		if (type.IsValueType || (type.IsClass & type.IsSealed))
		{
			// check for [ImmutableObject(true)]; note we're looking at this as a statement about
			// the overall nullability; for example, a type could contain a private int[] field,
			// where the field is mutable and the list is mutable; but if the type is annotated:
			// we're trusting that the API and use-case is such that the type is immutable
			return type.GetCustomAttribute<ImmutableObjectAttribute>() is { Immutable: true };
		}

		// don't trust interfaces and non-sealed types; we might have any concrete
		// type that has different behaviour
		return false;
	}
}
