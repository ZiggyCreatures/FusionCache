using System.Buffers;
using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Extensions.Caching.Distributed;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

/// <summary>
/// A fake IDistributedCache mimicking the classic RedisCache allocation profile:
/// Get allocates a fresh byte[] per call (like a value read from the network),
/// Set retains the passed array (like a write to the network: no extra allocation).
/// </summary>
public sealed class FakeClassicDistributedCache : IDistributedCache
{
	private readonly ConcurrentDictionary<string, byte[]> _store = new();

	public byte[]? Get(string key)
	{
		if (_store.TryGetValue(key, out var stored) == false)
			return null;

		var result = new byte[stored.Length];
		stored.CopyTo(result, 0);
		return result;
	}

	public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

	public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;

	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		Set(key, value, options);
		return Task.CompletedTask;
	}

	public void Refresh(string key) { }
	public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
	public void Remove(string key) => _store.TryRemove(key, out _);
	public Task RemoveAsync(string key, CancellationToken token = default)
	{
		Remove(key);
		return Task.CompletedTask;
	}
}

/// <summary>
/// A fake IBufferDistributedCache mimicking the buffered RedisCache allocation profile:
/// TryGet copies the stored payload into the caller's writer (like RedisCache copying from a pooled Lease),
/// Set copies the sequence into a rented array (like RedisCache writing a single-segment sequence to the network,
/// here we must retain a copy to act as the store).
/// </summary>
public sealed class FakeBufferDistributedCache : IBufferDistributedCache
{
	private sealed class Entry
	{
		public byte[] Buffer = [];
		public int Length;
	}

	private readonly ConcurrentDictionary<string, Entry> _store = new();

	public bool TryGet(string key, IBufferWriter<byte> destination)
	{
		if (_store.TryGetValue(key, out var entry) == false)
			return false;

		lock (entry)
		{
			destination.Write(entry.Buffer.AsSpan(0, entry.Length));
		}
		return true;
	}

	public ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token = default)
		=> new(TryGet(key, destination));

	public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
	{
		var entry = _store.GetOrAdd(key, static _ => new Entry());
		lock (entry)
		{
			var length = checked((int)value.Length);
			if (entry.Buffer.Length < length)
			{
				if (entry.Buffer.Length > 0)
					ArrayPool<byte>.Shared.Return(entry.Buffer);
				entry.Buffer = ArrayPool<byte>.Shared.Rent(length);
			}
			value.CopyTo(entry.Buffer);
			entry.Length = length;
		}
	}

	public ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		Set(key, value, options);
		return default;
	}

	public byte[]? Get(string key)
	{
		if (_store.TryGetValue(key, out var entry) == false)
			return null;
		lock (entry)
		{
			return entry.Buffer.AsSpan(0, entry.Length).ToArray();
		}
	}

	public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Set(key, new ReadOnlySequence<byte>(value), options);
	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
	{
		Set(key, value, options);
		return Task.CompletedTask;
	}
	public void Refresh(string key) { }
	public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
	public void Remove(string key) => _store.TryRemove(key, out _);
	public Task RemoveAsync(string key, CancellationToken token = default)
	{
		Remove(key);
		return Task.CompletedTask;
	}
}

[Config(typeof(Config))]
public class BufferedL2EndToEndBenchmark
{
	public class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
			AddDiagnoser(MemoryDiagnoser.Default);
			AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams);
			AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));
			WithOrderer(new DefaultOrderer(summaryOrderPolicy: SummaryOrderPolicy.Declared));
		}
	}

	[Params(10, 700, 30_000)]
	public int ModelCount;

	private List<SampleModel> _models = [];
	private FusionCache _classicCache = null!;
	private FusionCache _bufferedCache = null!;

	private static readonly FusionCacheEntryOptions _l2OnlyOptions = new()
	{
		Duration = TimeSpan.FromHours(24),
		SkipMemoryCacheRead = true,
		SkipMemoryCacheWrite = true,
	};

	private static FusionCache CreateCache(string name, IDistributedCache distributedCache)
	{
		var cache = new FusionCache(new FusionCacheOptions
		{
			CacheName = name,
		});
		cache.SetupSerializer(new FusionCacheSystemTextJsonSerializer());
		cache.SetupDistributedCache(distributedCache);
		return cache;
	}

	[GlobalSetup]
	public void Setup()
	{
		_models = [];
		for (var i = 0; i < ModelCount; i++)
		{
			_models.Add(SampleModel.GenerateRandom());
		}

		_classicCache = CreateCache("classic", new FakeClassicDistributedCache());
		_bufferedCache = CreateCache("buffered", new FakeBufferDistributedCache());

		// PRE-POPULATE FOR THE GET BENCHMARKS + ROUND-TRIP SANITY CHECK
		_classicCache.Set("key", _models, _l2OnlyOptions);
		_bufferedCache.Set("key", _models, _l2OnlyOptions);

		var fromClassic = _classicCache.GetOrDefault<List<SampleModel>>("key", options: _l2OnlyOptions);
		var fromBuffered = _bufferedCache.GetOrDefault<List<SampleModel>>("key", options: _l2OnlyOptions);
		if (fromClassic?.Count != ModelCount || fromBuffered?.Count != ModelCount || fromBuffered[0].Name != _models[0].Name)
			throw new InvalidOperationException("Round-trip sanity check failed.");
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_classicCache.Dispose();
		_bufferedCache.Dispose();
	}

	[Benchmark(Baseline = true)]
	public async Task Set_Classic()
	{
		await _classicCache.SetAsync("key", _models, _l2OnlyOptions).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task Set_Buffered()
	{
		await _bufferedCache.SetAsync("key", _models, _l2OnlyOptions).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task<int> Get_Classic()
	{
		var result = await _classicCache.GetOrDefaultAsync<List<SampleModel>>("key", options: _l2OnlyOptions).ConfigureAwait(false);
		return result!.Count;
	}

	[Benchmark]
	public async Task<int> Get_Buffered()
	{
		var result = await _bufferedCache.GetOrDefaultAsync<List<SampleModel>>("key", options: _l2OnlyOptions).ConfigureAwait(false);
		return result!.Count;
	}
}
