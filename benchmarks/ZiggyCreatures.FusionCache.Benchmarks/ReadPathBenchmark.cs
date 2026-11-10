using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

/// <summary>
/// Read path (cache hits) benchmark, with tagging enabled (the default):
/// <br/>- L1: memory-only, L1 hit
/// <br/>- L1Tagged: memory-only, L1 hit on an entry with tags
/// <br/>- L2: L1 + L2 (MemoryDistributedCache + SystemTextJson), L1 read skipped so every read is an L2 hit
/// <br/>
/// <br/>To add a scenario: add a <see cref="ReadScenario"/> value and configure it in <see cref="Setup"/>.
/// <br/>Culture defaults to en-US: set FUSIONCACHE_BENCH_CULTURES (e.g. "en-US,it-IT") to run more.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class ReadPathBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
		}
	}

	public enum ReadScenario
	{
		L1,
		L1Tagged,
		L2,
	}

	const string Key = "product:catalog:eu-west:item:00012345678";
	const string Value = "test value";
	static readonly string[] Tags = ["tag-catalog", "tag-eu-west"];

	public static IEnumerable<string> Cultures()
	{
		var cultures = Environment.GetEnvironmentVariable("FUSIONCACHE_BENCH_CULTURES");
		if (string.IsNullOrWhiteSpace(cultures))
			return ["en-US"];

		return cultures.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	[ParamsSource(nameof(Cultures))]
	public string Culture { get; set; } = "en-US";

	[ParamsAllValues]
	public ReadScenario Scenario { get; set; }

	private FusionCache _cache = null!;
	private FusionCacheEntryOptions _options = null!;
	private CultureInfo _originalCulture = null!;
	private CultureInfo? _originalDefaultCulture;

	[GlobalSetup]
	public void Setup()
	{
		// THE IN-PROCESS TOOLCHAIN RUNS EVERY BENCHMARK IN THE SAME PROCESS: RESTORE THE CULTURE IN CLEANUP
		_originalCulture = CultureInfo.CurrentCulture;
		_originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;

		var culture = BenchmarkCultures.Create(Culture);
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.CurrentCulture = culture;

		var options = new FusionCacheOptions();
		options.DefaultEntryOptions.Duration = TimeSpan.FromDays(1);
		IEnumerable<string>? tags = null;

		switch (Scenario)
		{
			case ReadScenario.L1:
				break;
			case ReadScenario.L1Tagged:
				tags = Tags;
				break;
			case ReadScenario.L2:
				// ON THE DEFAULT ENTRY OPTIONS, SO OVERLOADS STARTING FROM THEM (DURATION, SETUP LAMBDA) SKIP L1 TOO
				options.DefaultEntryOptions.SkipMemoryCacheRead = true;
				break;
		}

		_cache = new FusionCache(options);

		if (Scenario == ReadScenario.L2)
		{
			_cache.SetupDistributedCache(
				new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
				new FusionCacheSystemTextJsonSerializer()
			);
		}

		_options = _cache.DefaultEntryOptions.Duplicate();

		_cache.Set(Key, Value, _options, tags);

		// WARM UP TAG/CLEAR INTERNAL ENTRIES
		_ = TryGet();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_cache.Dispose();

		CultureInfo.CurrentCulture = _originalCulture;
		CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
	}

	[Benchmark]
	public ValueTask<string> GetOrSetAsync() => _cache.GetOrSetAsync<string>(Key, static (_, _) => Task.FromResult(Value), default, _options);

	[Benchmark]
	public string GetOrSet() => _cache.GetOrSet<string>(Key, static (_, _) => Value, default, _options);

	[Benchmark]
	public async ValueTask<string?> TryGetAsync() => (await _cache.TryGetAsync<string?>(Key, _options)).GetValueOrDefault(null);

	[Benchmark]
	public string? TryGet() => _cache.TryGet<string?>(Key, _options).GetValueOrDefault(null);

	[Benchmark]
	public ValueTask<string?> GetOrDefaultAsync() => _cache.GetOrDefaultAsync<string?>(Key, null, _options);

	[Benchmark]
	public string? GetOrDefault() => _cache.GetOrDefault<string?>(Key, null, _options);
}
