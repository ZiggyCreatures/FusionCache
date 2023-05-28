using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Internals.Memory;

namespace ZiggyCreatures.Caching.Fusion.Internals;

internal static class FusionCacheInternalUtils
{
	private static readonly char[] _chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();
	private static long _lastId = DateTime.UtcNow.Ticks;
	private static readonly ThreadLocal<char[]> _buffer = new ThreadLocal<char[]>(() => new char[13]);
	private static readonly DateTimeOffset DateTimeOffsetMaxValue = DateTimeOffset.MaxValue;
	private static readonly TimeSpan TimeSpanMaxValue = TimeSpan.MaxValue;

	public static string GenerateOperationId_V1()
	{
		return Guid.NewGuid().ToString("N");
	}

	private static string GenerateOperationId_V2(long id)
	{
		var buffer = new char[13];

		buffer[12] = _chars[id & 31];
		buffer[11] = _chars[(id >> 5) & 31];
		buffer[10] = _chars[(id >> 10) & 31];
		buffer[9] = _chars[(id >> 15) & 31];
		buffer[8] = _chars[(id >> 20) & 31];
		buffer[7] = _chars[(id >> 25) & 31];
		buffer[6] = _chars[(id >> 30) & 31];
		buffer[5] = _chars[(id >> 35) & 31];
		buffer[4] = _chars[(id >> 40) & 31];
		buffer[3] = _chars[(id >> 45) & 31];
		buffer[2] = _chars[(id >> 50) & 31];
		buffer[1] = _chars[(id >> 55) & 31];
		buffer[0] = _chars[(id >> 60) & 31];

		return new string(buffer);
	}

	public static string GenerateOperationId_V2()
	{
		return GenerateOperationId_V2(Interlocked.Increment(ref _lastId));
	}

	private static string GenerateOperationId_V3(long id)
	{
		// SEE: https://nimaara.com/2018/10/10/generating-ids-in-csharp.html

		char[] buffer = _buffer.Value;

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

	public static string GenerateOperationId_V3()
	{
		return GenerateOperationId_V3(Interlocked.Increment(ref _lastId));
	}

	public static string MaybeGenerateOperationId(ILogger? logger)
	{
		if (logger is null)
			return string.Empty;

		return GenerateOperationId_V3();
	}

	/// <summary>
	/// Checks if the entry is logically expired.
	/// </summary>
	/// <returns>A <see cref="bool"/> indicating the logical expiration status.</returns>
	public static bool IsLogicallyExpired(this IFusionCacheEntry? entry)
	{
		if (entry?.Metadata is null)
			return false;

		return entry.Metadata.IsLogicallyExpired();
	}

	public static readonly Type CacheItemPriorityType = typeof(CacheItemPriority);

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

		return dt.ToString("o");
	}

	public static string? ToLogString_Expiration(this DateTimeOffset? dt)
	{
		if (dt.HasValue == false)
			return "/";

		return dt.Value.ToLogString_Expiration();
	}

	public static string? ToLogString(this DateTimeOffset? dt)
	{
		if (dt is null)
			return "/";

		return dt.Value.ToString("o");
	}

	public static string? ToLogString(this MemoryCacheEntryOptions? options)
	{
		if (options is null)
			return null;

		return $"MEO[CEXP={options.AbsoluteExpiration!.Value.ToLogString_Expiration()} PR={options.Priority.ToLogString()} S={options.Size?.ToString()}]";
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

	public static string? ToLogString(this IFusionCacheEntry? entry)
	{
		if (entry is null)
			return null;

		return "FE" + entry.ToString();
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
		switch (priority)
		{
			case CacheItemPriority.Low:
				return "L";
			case CacheItemPriority.Normal:
				return "N";
			case CacheItemPriority.High:
				return "H";
			case CacheItemPriority.NeverRemove:
				return "NR";
		}

		// FALLBACK
		return Enum.GetName(CacheItemPriorityType, priority);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ToStringYN(this bool b)
	{
		return b ? "Y" : "N";
	}

	public static FusionCacheDistributedEntry<TValue> AsDistributedEntry<TValue>(this IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (entry is FusionCacheDistributedEntry<TValue>)
			return (FusionCacheDistributedEntry<TValue>)entry;

		return FusionCacheDistributedEntry<TValue>.CreateFromOptions(entry.GetValue<TValue>(), options, entry.Metadata?.IsFromFailSafe ?? false, entry.Metadata?.LastModified, entry.Metadata?.ETag);
	}

	public static FusionCacheMemoryEntry AsMemoryEntry(this IFusionCacheEntry entry, FusionCacheEntryOptions options)
	{
		if (entry is FusionCacheMemoryEntry)
			return (FusionCacheMemoryEntry)entry;

		return FusionCacheMemoryEntry.CreateFromOptions(entry.GetValue<object>(), options, entry.Metadata?.IsFromFailSafe ?? false, entry.Metadata?.LastModified, entry.Metadata?.ETag);
	}

	public static void SafeExecute<TEventArgs>(this EventHandler<TEventArgs> ev, string? operationId, string? key, IFusionCache cache, Func<TEventArgs> eventArgsBuilder, string eventName, ILogger? logger, LogLevel logLevel, bool syncExecution)
	{
		static void ExecuteInvocations(string? operationId, string? key, IFusionCache cache, string eventName, TEventArgs e, Delegate[] invocations, ILogger? logger, LogLevel errorLogLevel)
		{
			foreach (EventHandler<TEventArgs> invocation in invocations)
			{
				try
				{
					invocation(cache, e);
				}
				catch (Exception exc)
				{
					logger?.Log(errorLogLevel, exc, "FUSION (O={CacheOperationId} K={CacheKey}): an error occurred while handling an event handler for {EventName}", operationId, key, eventName);
				}
			}
		}

		var invocations = ev.GetInvocationList();

		// WE ONLY TEST IF THE LOG LEVEL IS ENABLED ONCE: IN THAT CASE WE'LL USE THE LOGGER, OTHERWISE WE SET IT TO null TO AVOID CHECKING IT EVERY TIME INSIDE THE LOOP
		if (logger is not null && logger.IsEnabled(logLevel) == false)
			logger = null;

		var e = eventArgsBuilder();

		if (syncExecution)
		{
			ExecuteInvocations(operationId, key, cache, eventName, e, invocations, logger, logLevel);
		}
		else
		{
			Task.Run(() => ExecuteInvocations(operationId, key, cache, eventName, e, invocations, logger, logLevel));
		}
	}

	public static string GetBackplaneChannelName(this FusionCacheOptions options)
	{
		var prefix = options.BackplaneChannelPrefix;
		if (string.IsNullOrWhiteSpace(prefix))
			prefix = options.CacheName;

		// SAFETY NET (BUT IT SHOULD NOT HAPPEN)
		if (string.IsNullOrWhiteSpace(prefix))
			prefix = "FusionCache";

		return $"{prefix}.Backplane";
	}

	public static DateTimeOffset GetNormalizedAbsoluteExpiration(TimeSpan duration, FusionCacheEntryOptions options, bool allowJittering)
	{
		// SHORT CIRCUIT: COMMON CASE FOR WHEN USERS DO NOT WANT EXPIRATION
		if (duration == TimeSpanMaxValue)
			return DateTimeOffsetMaxValue;

		if (allowJittering && options.JitterMaxDuration > TimeSpan.Zero)
		{
			// SHORT CIRCUIT: WHEN THE VALUES ARE NOT THE LIMITS BUT ARE STRETCHED VERY NEAR THEM
			if (duration > (TimeSpanMaxValue - options.JitterMaxDuration))
				return DateTimeOffsetMaxValue;

			// ADD JITTERING
			duration += TimeSpan.FromMilliseconds(options.GetJitterDurationMs());
		}

		// SHORT CIRCUIT: WHEN OVERFLOWING DateTimeOffset.MaxValue
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
}
