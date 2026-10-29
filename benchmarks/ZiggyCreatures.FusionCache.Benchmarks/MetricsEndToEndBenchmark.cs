using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

/// <summary>
/// Measures the real end-to-end cost of the metrics path on hot FusionCache operations,
/// by comparing the same operation with and without a MeterListener attached.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared)]
public class MetricsEndToEndBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddDiagnoser(BenchmarkDotNet.Diagnosers.MemoryDiagnoser.Default);
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
		}
	}

	private const string Key = "test key";
	private const string Value = "test value";

	private readonly FusionCache _cache = new(new FusionCacheOptions { CacheName = "FusionCache" });
	private MeterListener? _listener;
	private long _sink;

	[Params(false, true)]
	public bool WithListener { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_cache.Set(Key, Value);

		if (WithListener == false)
			return;

		_listener = new MeterListener();
		_listener.InstrumentPublished = (i, l) =>
		{
			if (i.Meter.Name.StartsWith(FusionCacheDiagnostics.MeterName, StringComparison.Ordinal))
				l.EnableMeasurementEvents(i);
		};
		_listener.SetMeasurementEventCallback<long>((_, m, tags, _) => _sink += m + tags.Length);
		_listener.Start();
	}

	[GlobalCleanup]
	public void Cleanup() => _listener?.Dispose();

	[Benchmark]
	public string? TryGet() => _cache.TryGet<string?>(Key).GetValueOrDefault(null);

	[Benchmark]
	public string? GetOrDefault() => _cache.GetOrDefault<string>(Key);

	[Benchmark]
	public string GetOrSet() => _cache.GetOrSet<string>(Key, (_, _) => Value);

	[Benchmark]
	public async Task<string> GetOrSetAsync() => await _cache.GetOrSetAsync<string>(Key, (_, _) => Task.FromResult(Value));

	[Benchmark]
	public void Set() => _cache.Set(Key, Value);
}
