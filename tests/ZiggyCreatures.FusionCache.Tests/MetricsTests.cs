using System.Diagnostics.Metrics;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace ZiggyCreatures.Caching.Fusion.Tests;

public class MetricsTests
{
	private const string CacheNameTagName = "fusioncache.cache.name";

	private sealed class Recorder : IDisposable
	{
		private readonly MeterListener _listener;
		private readonly string _cacheName;
		private readonly List<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];

		public Recorder(string cacheName)
		{
			_cacheName = cacheName;

			_listener = new MeterListener
			{
				InstrumentPublished = (i, l) =>
				{
					if (i.Meter.Name.StartsWith(FusionCacheDiagnostics.MeterName, StringComparison.Ordinal))
						l.EnableMeasurementEvents(i);
				}
			};
			_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
			{
				foreach (var tag in tags)
				{
					if (tag.Key == CacheNameTagName && (tag.Value as string) == _cacheName)
					{
						lock (_measurements)
						{
							_measurements.Add((instrument.Name, measurement, tags.ToArray()));
						}
						return;
					}
				}
			});
			_listener.Start();
		}

		public void Clear()
		{
			lock (_measurements)
			{
				_measurements.Clear();
			}
		}

		public (string Instrument, long Value, KeyValuePair<string, object?>[] Tags) Single(string instrumentName)
		{
			lock (_measurements)
			{
				return _measurements.Single(x => x.Instrument == instrumentName);
			}
		}

		public void Dispose() => _listener.Dispose();
	}

	private static string CreateCacheName() => "MetricsTests-" + Guid.NewGuid().ToString("N");

	[Fact]
	public void CountersAreTaggedWithTheCacheName()
	{
		var cacheName = CreateCacheName();

		using var recorder = new Recorder(cacheName);
		using var cache = new FusionCache(new FusionCacheOptions { CacheName = cacheName });

		cache.Set<int>("k", 42, token: TestContext.Current.CancellationToken);

		var m = recorder.Single("fusioncache.cache.set");

		Assert.Equal(1, m.Value);
		Assert.Equal([new KeyValuePair<string, object?>(CacheNameTagName, cacheName)], m.Tags);
	}

	[Fact]
	public void CountersWithAnExtraTagKeepBothTagsAndTheirOrder()
	{
		var cacheName = CreateCacheName();

		using var recorder = new Recorder(cacheName);
		using var cache = new FusionCache(new FusionCacheOptions { CacheName = cacheName });

		cache.Set<int>("k", 42, token: TestContext.Current.CancellationToken);
		recorder.Clear();

		cache.TryGet<int>("k", token: TestContext.Current.CancellationToken);

		foreach (var name in new[] { "fusioncache.cache.hit", "fusioncache.memory.hit" })
		{
			var m = recorder.Single(name);

			Assert.Equal(1, m.Value);
			Assert.Equal(
				[
					new KeyValuePair<string, object?>(CacheNameTagName, cacheName),
					new KeyValuePair<string, object?>("fusioncache.stale", false)
				],
				m.Tags
			);
		}
	}

	[Fact]
	public void RemoveByTagDoesNotIncludeTheTagWhenNotEnabled()
	{
		var cacheName = CreateCacheName();

		using var recorder = new Recorder(cacheName);
		using var cache = new FusionCache(new FusionCacheOptions { CacheName = cacheName, IncludeTagsInMetrics = false });

		cache.RemoveByTag("t1", token: TestContext.Current.CancellationToken);

		var m = recorder.Single("fusioncache.cache.remove_by_tag");

		Assert.Equal([new KeyValuePair<string, object?>(CacheNameTagName, cacheName)], m.Tags);
	}

	[Fact]
	public void RemoveByTagIncludesTheTagWhenEnabled()
	{
		var cacheName = CreateCacheName();

		using var recorder = new Recorder(cacheName);
		using var cache = new FusionCache(new FusionCacheOptions { CacheName = cacheName, IncludeTagsInMetrics = true });

		cache.RemoveByTag("t1", token: TestContext.Current.CancellationToken);

		var m = recorder.Single("fusioncache.cache.remove_by_tag");

		Assert.Equal(
			[
				new KeyValuePair<string, object?>(CacheNameTagName, cacheName),
				new KeyValuePair<string, object?>("fusioncache.operation.tag", "t1")
			],
			m.Tags
		);
	}

	[Fact]
	public void BoolTagValuesAreNotBoxedPerCall()
	{
		var a = Tags.Tag("x", true).Value;
		var b = Tags.Tag("y", true).Value;

		Assert.Same(a, b);
	}
}
