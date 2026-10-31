using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

/// <summary>
/// Compares strategies for emitting a Counter measurement with the "common tags" (cache name) + optional extra tags,
/// WITH a MeterListener attached (otherwise none of this code runs at all).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class MetricsTagsBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddDiagnoser(BenchmarkDotNet.Diagnosers.MemoryDiagnoser.Default);
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
		}
	}

	private const string CacheNameTag = "fusioncache.cache.name";
	private const string StaleTag = "fusioncache.stale";
	private const string CacheName = "FusionCache";
	private const string InstanceId = "0a1b2c3d4e5f6";

	private static readonly Meter Meter = new("bench.meter", "1.0.0");
	private static readonly Counter<long> Counter = Meter.CreateCounter<long>("bench.counter");
	private static readonly Meter DisabledMeter = new("bench.meter.disabled", "1.0.0");
	private static readonly Counter<long> DisabledCounter = DisabledMeter.CreateCounter<long>("bench.counter.disabled");

	private MeterListener _listener = null!;
	private long _sink;

	// pre-baked, option A
	private static readonly KeyValuePair<string, object?>[] PreBaked = [new(CacheNameTag, CacheName)];
	private static readonly KeyValuePair<string, object?> PreBakedSingle = new(CacheNameTag, CacheName);
	private static readonly object BoxedTrue = true;
	private static readonly object BoxedFalse = false;

	[GlobalSetup]
	public void Setup()
	{
		_listener = new MeterListener();
		_listener.InstrumentPublished = (i, l) =>
		{
			if (i.Meter == Meter)
				l.EnableMeasurementEvents(i);
		};
		_listener.SetMeasurementEventCallback<long>((_, m, tags, _) =>
		{
			_sink += m + tags.Length;
		});
		_listener.Start();

		if (Counter.Enabled == false)
			throw new InvalidOperationException("listener not attached");
	}

	[GlobalCleanup]
	public void Cleanup() => _listener.Dispose();

	// ---------- CURRENT IMPLEMENTATION (copied from Metrics.cs) ----------

	private static KeyValuePair<string, object?>[] GetCommonTags(string? cacheName, string? cacheInstanceId, params KeyValuePair<string, object?>[] extraTags)
	{
		return [
			new KeyValuePair<string, object?>(CacheNameTag, cacheName),
			.. extraTags ?? []
		];
	}

	private static void AddWithCommonTags_Current<T>(Counter<T> counter, T delta, string? cacheName, string? cacheInstanceId, params KeyValuePair<string, object?>[] extraTags)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		counter.Add(delta, GetCommonTags(cacheName, cacheInstanceId, extraTags));
	}

	// ---------- OPTION B: TagList ----------

	private static void AddWithCommonTags_TagList<T>(Counter<T> counter, T delta, string? cacheName, string? cacheInstanceId)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		var tags = new TagList { { CacheNameTag, cacheName } };
		counter.Add(delta, in tags);
	}

	private static void AddWithCommonTags_TagList<T>(Counter<T> counter, T delta, string? cacheName, string? cacheInstanceId, KeyValuePair<string, object?> extraTag)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		var tags = new TagList { { CacheNameTag, cacheName }, extraTag };
		counter.Add(delta, in tags);
	}

	private static void AddWithCommonTags_TagListNoBox<T>(Counter<T> counter, T delta, string? cacheName, string? cacheInstanceId, string extraTagName, bool extraTagValue)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		var tags = new TagList { { CacheNameTag, cacheName }, { extraTagName, extraTagValue ? BoxedTrue : BoxedFalse } };
		counter.Add(delta, in tags);
	}

	// ---------- OPTION A: pre-baked array ----------

	private static void AddWithCommonTags_PreBaked<T>(Counter<T> counter, T delta, KeyValuePair<string, object?>[] common)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		counter.Add(delta, common);
	}

	// =============== BENCHMARKS: no extra tags ===============

	[BenchmarkCategory("NoExtraTags"), Benchmark(Baseline = true)]
	public void NoExtra_Current() => AddWithCommonTags_Current(Counter, 1L, CacheName, InstanceId);

	[BenchmarkCategory("NoExtraTags"), Benchmark]
	public void NoExtra_TagList() => AddWithCommonTags_TagList(Counter, 1L, CacheName, InstanceId);

	[BenchmarkCategory("NoExtraTags"), Benchmark]
	public void NoExtra_PreBakedArray() => AddWithCommonTags_PreBaked(Counter, 1L, PreBaked);

	[BenchmarkCategory("NoExtraTags"), Benchmark]
	public void NoExtra_SingleKvpOverload() => Counter.Add(1L, PreBakedSingle);

	// =============== BENCHMARKS: one extra tag (bool) ===============

	[BenchmarkCategory("OneExtraTag"), Benchmark]
	public void OneExtra_Current() => AddWithCommonTags_Current(Counter, 1L, CacheName, InstanceId, new KeyValuePair<string, object?>(StaleTag, false));

	[BenchmarkCategory("OneExtraTag"), Benchmark]
	public void OneExtra_TagList() => AddWithCommonTags_TagList(Counter, 1L, CacheName, InstanceId, new KeyValuePair<string, object?>(StaleTag, false));

	[BenchmarkCategory("OneExtraTag"), Benchmark]
	public void OneExtra_TagListNoBox() => AddWithCommonTags_TagListNoBox(Counter, 1L, CacheName, InstanceId, StaleTag, false);

	// ---------- OPTION C: pre-baked common tag + native multi-tag Add overloads ----------

	private static void AddWithCommonTags_OptionC<T>(Counter<T> counter, T delta, in KeyValuePair<string, object?> common)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		counter.Add(delta, common);
	}

	private static void AddWithCommonTags_OptionC<T>(Counter<T> counter, T delta, in KeyValuePair<string, object?> common, KeyValuePair<string, object?> extraTag)
		where T : struct
	{
		if (counter.Enabled == false)
			return;

		counter.Add(delta, common, extraTag);
	}

	[BenchmarkCategory("NoExtraTags"), Benchmark]
	public void NoExtra_OptionC() => AddWithCommonTags_OptionC(Counter, 1L, in PreBakedSingle);

	[BenchmarkCategory("OneExtraTag"), Benchmark]
	public void OneExtra_OptionC() => AddWithCommonTags_OptionC(Counter, 1L, in PreBakedSingle, new KeyValuePair<string, object?>(StaleTag, false));

	[BenchmarkCategory("OneExtraTag"), Benchmark]
	public void OneExtra_OptionC_NoBox() => AddWithCommonTags_OptionC(Counter, 1L, in PreBakedSingle, new KeyValuePair<string, object?>(StaleTag, BoxedFalse));

	[BenchmarkCategory("Disabled"), Benchmark]
	public void Disabled_NoListener() => AddWithCommonTags_Current(DisabledCounter, 1L, CacheName, InstanceId);
}
