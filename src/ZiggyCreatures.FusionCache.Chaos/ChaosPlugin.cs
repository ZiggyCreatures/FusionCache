using System;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Chaos.Internals;
using ZiggyCreatures.Caching.Fusion.Plugins;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// An implementation of <see cref="IFusionCachePlugin"/> with a (controllable) amount of chaos in-between.
	/// </summary>
	public class ChaosPlugin
		: AbstractChaosComponent
		, IFusionCachePlugin
	{
		IFusionCachePlugin _innerPlugin;

		/// <summary>
		/// Initializes a new instance of the ChaosPlugin class.
		/// </summary>
		/// <param name="innerPlugin">The actual <see cref="IFusionCachePlugin"/> used if and when chaos does not happen.</param>
		/// <param name="logger">The logger to use, or <see langword="null"/>.</param>
		public ChaosPlugin(IFusionCachePlugin innerPlugin, ILogger<ChaosPlugin>? logger = null)
			: base(logger)
		{
			_innerPlugin = innerPlugin ?? throw new ArgumentNullException(nameof(innerPlugin));
		}

		/// <inheritdoc/>
		public void Start(IFusionCache cache)
		{
			MaybeChaos();
			_innerPlugin.Start(cache);
		}

		/// <inheritdoc/>
		public void Stop(IFusionCache cache)
		{
			MaybeChaos();
			_innerPlugin.Stop(cache);
		}
	}
}
