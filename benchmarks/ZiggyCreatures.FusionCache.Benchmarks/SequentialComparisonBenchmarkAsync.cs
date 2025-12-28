using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using CacheTower;
using CacheTower.Extensions;
using CacheTower.Providers.Memory;
using EasyCaching.Core;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class SequentialComparisonBenchmarkAsync
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
	private CacheStack _CacheTower = null!;
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
		var builder = Host.CreateDefaultBuilder();
		builder.ConfigureServices(services =>
		{
			services.AddEasyCaching(options => { options.UseInMemory("default"); });
		});
		var host = builder.Build();

		ServiceProvider = host.Services;

		// SETUP CACHES
		_FusionCache = new FusionCache(new FusionCacheOptions { DefaultEntryOptions = new FusionCacheEntryOptions(CacheDuration) });
		_CacheTower = new CacheStack(null, new CacheStackOptions([new MemoryCacheLayer()]) { Extensions = [new AutoCleanupExtension(TimeSpan.FromMinutes(5))] });
		_EasyCaching = ServiceProvider.GetRequiredService<IEasyCachingProviderFactory>().GetCachingProvider("default");
		_LazyCache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())));
		_LazyCache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = (int)(CacheDuration.TotalSeconds) };
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_FusionCache.Dispose();
		_CacheTower.DisposeAsync().AsTask().Wait();
	}

	[Benchmark(Baseline = true)]
	public async Task FusionCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				await _FusionCache.GetOrSetAsync<SamplePayload>(
					key,
					async ct =>
					{
						return new SamplePayload();
					}
				);
			}
		}

		// NO NEED TO CLEANUP, AUTOMATICALLY DONE WHEN DISPOSING
	}

	[Benchmark]
	public async Task CacheTower()
	{
		var cacheSettings = new CacheSettings(CacheDuration, CacheDuration);

		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				await _CacheTower.GetOrSetAsync<SamplePayload>(
					key,
					async (old) =>
					{
						return new SamplePayload();
					},
					cacheSettings
				);
			}
		}
	}

	[Benchmark]
	public async Task EasyCaching()
	{
		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				await _EasyCaching.GetAsync<SamplePayload>(
					key,
					async () =>
					{
						return new SamplePayload();
					},
					CacheDuration
				);
			}
		}
	}

	[Benchmark]
	public async Task LazyCache()
	{
		for (int i = 0; i < Rounds; i++)
		{
			foreach (var key in Keys)
			{
				await _LazyCache.GetOrAddAsync<SamplePayload>(
					key,
					async () =>
					{
						return new SamplePayload();
					}
				);
			}
		}
	}
}
