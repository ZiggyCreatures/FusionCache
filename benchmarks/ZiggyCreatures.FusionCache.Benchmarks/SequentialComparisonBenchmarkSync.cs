﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using EasyCaching.Core;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class SequentialComparisonBenchmarkSync
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
			AddDiagnoser(MemoryDiagnoser.Default);
			//AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByMethod);
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
			//WithOrderer(new DefaultOrderer(summaryOrderPolicy: SummaryOrderPolicy.FastestToSlowest));
			WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default.WithMaxParameterColumnWidth(50));
		}
	}

	[Params(200)]
	public int KeysCount;

	[Params(1, 50)]
	public int Rounds;

	private List<string> Keys = null!;
	private TimeSpan CacheDuration = TimeSpan.FromDays(10);
	private IServiceProvider ServiceProvider = null!;

	private FusionCache _FusionCache = null!;
	private IEasyCachingProvider _EasyCaching = null!;
	private CachingService _LazyCache = null!;

	[GlobalSetup]
	public void Setup()
	{
		// SETUP KEYS
		Keys = [];
		for (int i = 0; i < KeysCount; i++)
		{
			var key = Guid.NewGuid().ToString("N") + "-" + i.ToString();
			Keys.Add(key);
		}

		// SETUP DI
		var services = new ServiceCollection();
		services.AddEasyCaching(options => { options.UseInMemory("default"); });
		ServiceProvider = services.BuildServiceProvider();

		// SETUP CACHES
		_FusionCache = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) });
		_EasyCaching = ServiceProvider.GetRequiredService<IEasyCachingProviderFactory>().GetCachingProvider("default");
		_LazyCache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())));
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_FusionCache.Dispose();
	}

	[Benchmark(Baseline = true)]
	public void FusionCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				_FusionCache.GetOrSet<SamplePayload>(
				   key,
				   ct =>
				   {
					   return new SamplePayload();
				   }
			   );
			}
		}
	}

	[Benchmark]
	public void EasyCaching()
	{
		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				_EasyCaching.Get<SamplePayload>(
					key,
					() =>
					{
						return new SamplePayload();
					},
					CacheDuration
				);
			}
		}
	}

	[Benchmark]
	public void LazyCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				_LazyCache.GetOrAdd<SamplePayload>(
				   key,
				   () =>
				   {
					   return new SamplePayload();
				   }
			   );
			}
		}
	}
}
