using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZiggyCreatures.FusionCaching.Internals
{

	internal static class FusionCacheInternalUtils
	{

		public static Random RNG = new Random();

		public static readonly Type CacheItemPriorityType = typeof(CacheItemPriority);

		public static string GetCurrentMemberName([CallerMemberName] string name = "")
		{
			return name;
		}

		public static Exception GetSingleInnerExceptionOrSelf(this AggregateException exc)
		{
			// TODO: NOT SURE ABOUT THIS
			//if (exc is null)
			//	return null;
			return (exc.InnerException is object && exc.InnerExceptions?.Count <= 1)
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

			return dt.ToString();
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

			return $"DEO[CEXP={options.AbsoluteExpiration?.ToLogString_Expiration()}s]";
		}

		public static string? ToLogString(this FusionCacheEntryOptions? options)
		{
			if (options is null)
				return null;

			return "FEO" + options.ToString();
		}

		public static string? ToLogString<TValue>(this FusionCacheEntry<TValue>? entry)
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
	}

}