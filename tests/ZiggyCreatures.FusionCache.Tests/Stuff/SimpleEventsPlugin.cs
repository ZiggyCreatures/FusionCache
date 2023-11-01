using System;
using System.Threading;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace FusionCacheTests.Stuff;

internal class SimpleEventsPlugin
		: IFusionCachePlugin
{
	private readonly bool _throwOnStart = false;
	private int _missCount = 0;

	public SimpleEventsPlugin(bool throwOnStart = false)
	{
		_throwOnStart = throwOnStart;
	}

	public void Start(IFusionCache cache)
	{
		IsStarted = true;

		if (_throwOnStart)
			throw new Exception("Uooops ¯\\_(ツ)_/¯");

		cache.Events.Miss += OnMiss;
	}

	public void Stop(IFusionCache cache)
	{
		IsStopped = true;
		cache.Events.Miss -= OnMiss;
	}

	private void OnMiss(object? sender, FusionCacheEntryEventArgs e)
	{
		Interlocked.Increment(ref _missCount);
	}

	public bool IsStarted { get; private set; }
	public bool IsStopped { get; private set; }

	public int MissCount
	{
		get { return _missCount; }
	}
}
