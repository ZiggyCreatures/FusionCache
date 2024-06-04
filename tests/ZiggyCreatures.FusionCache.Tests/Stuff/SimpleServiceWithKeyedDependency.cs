using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests.Stuff
{
	public class SimpleServiceWithKeyedDependency
	{
		public SimpleServiceWithKeyedDependency([FromKeyedServices("FooCache")] IFusionCache cache)
		{
			System.ArgumentNullException.ThrowIfNull(cache);

			Cache = cache;
		}

		public IFusionCache Cache { get; }
	}
}
