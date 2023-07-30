using ZiggyCreatures.Caching.Fusion.Plugins;

namespace ZiggyCreatures.Caching.Fusion.NullObjects
{
	/// <summary>
	/// An implementation of <see cref="IFusionCachePlugin"/> that implements the null object pattern, meaning that it does nothing.
	/// </summary>
	public class NullFusionCachePlugin
		: IFusionCachePlugin
	{
		/// <inheritdoc/>
		public void Start(IFusionCache cache)
		{
			// EMPTY
		}

		/// <inheritdoc/>
		public void Stop(IFusionCache cache)
		{
			// EMPTY
		}
	}
}
