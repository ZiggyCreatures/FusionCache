using System;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FastCache;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	[RankColumn]
	[MemoryDiagnoser]
	[ShortRunJob(RuntimeMoniker.Net60)]
	[ShortRunJob(RuntimeMoniker.Net80)]
	[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
	public class HappyPathBenchmark
	{
		const string Key = "test key";
		const string Value = "test value";

		readonly FusionCache FusionCache = new(new FusionCacheOptions());
		readonly MemoryCache MemoryCache = new(new MemoryCacheOptions());
		readonly CachingService LazyCache = new();

		[GlobalSetup]
		public void Setup()
		{
			FusionCache.Set(Key, Value);
			MemoryCache.Set(Key, Value);
			LazyCache.Add(Key, Value);
			Cached<string>.Save(Key, Value, TimeSpan.FromDays(1));
		}

		public class HappyPathReads : HappyPathBenchmark
		{
			[Benchmark(Baseline = true)]
			public string? GetFusionCache()
			{
				return FusionCache.TryGet<string?>(Key)
					.GetValueOrDefault(null);
			}

			[Benchmark]
			public string? GetMemoryCache()
			{
				return MemoryCache.TryGetValue<string?>(Key, out var value)
					? value
					: Unreachable();
			}

			[Benchmark]
			public string? GetLazyCache()
			{
				return LazyCache.TryGetValue<string?>(Key, out var value)
					? value
					: Unreachable();
			}

			[Benchmark]
			public string? GetFastCache()
			{
				return Cached<string?>.TryGet(Key, out var value)
					? value
					: Unreachable();
			}
		}

		public class HappyPathWrites : HappyPathBenchmark
		{
			[Benchmark(Baseline = true)]
			public void SetFusionCache() => FusionCache.Set(Key, Value);

			[Benchmark]
			public void SetMemoryCache() => MemoryCache.Set(Key, Value);

			[Benchmark]
			public void SetLazyCache() => LazyCache.Add(Key, Value);

			[Benchmark]
			public void SetFastCache() => Cached<string>.Save(Key, Value, TimeSpan.FromDays(1));
		}

		[DoesNotReturn]
		static string Unreachable() => throw new Exception("Unreachable code");
	}
}
