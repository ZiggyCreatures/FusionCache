using System.Diagnostics.Metrics;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

namespace FusionCacheTests.Stuff;

public sealed class MeterRecorder : IDisposable
{
	private readonly MeterListener _listener;
	private readonly string _cacheName;
	private readonly List<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];

	public MeterRecorder(string cacheName)
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
				if (tag.Key == Tags.Names.CacheName && (tag.Value as string) == _cacheName)
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
