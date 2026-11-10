using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

/// <summary>
/// Compares the culture-sensitive <see cref="string.StartsWith(string)"/> with the ordinal one,
/// for the internal tag key prefix check done on every read.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class TagPrefixCheckBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
		}
	}

	const string Prefix = FusionCacheInternalStrings.DefaultTagCacheKeyPrefix;

	[Params("en-US", "de-DE", "Invariant")]
	public string Culture { get; set; } = "en-US";

	[Params(10, 40, 120)]
	public int KeyLength { get; set; }

	[Params(false, true)]
	public bool IsTagKey { get; set; }

	private string _key = "";
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

		var head = IsTagKey ? Prefix : "product:";
		_key = (head + "catalog:eu-west:item:0001234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789").Substring(0, KeyLength);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		CultureInfo.CurrentCulture = _originalCulture;
		CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
	}

	[Benchmark(Baseline = true)]
	public bool CurrentCulture() => _key.StartsWith(Prefix);

	[Benchmark]
	public bool Ordinal() => _key.StartsWith(Prefix, StringComparison.Ordinal);
}
