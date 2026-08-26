using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion.Internals.Provider;

namespace ZiggyCreatures.Caching.Fusion.Internals.Builder;

internal class FusionCacheDependencyInjectionValidator
{
	private int _checksExecuted = 0;

	public void CheckInvalidRegistrations(IServiceCollection services, IServiceProvider serviceProvider, ILogger? logger)
	{
		if (services is null || serviceProvider is null || logger is null)
			return;

		// ENSURE IT RUNS ONLY ONCE
		if (Interlocked.CompareExchange(ref _checksExecuted, 1, 0) != 0)
			return;

		if ((logger?.IsEnabled(LogLevel.Warning) ?? false) == false)
			return;

		// CHECK FOR MULTIPLE IFusionCache REGISTRATIONS
		foreach (var group in services.Where(x => x.ServiceType == typeof(IFusionCache)).GroupBy(x => x.ServiceKey))
		{
			var count = group.Count();

			if (count <= 1)
				continue;

			if (group.Key is null)
			{
				// NON KEYED SERVICE
				logger.Log(LogLevel.Warning, "FUSION: multiple non keyed IFusionCache registrations ({Count}) have been detected. The last one will be used, as per Microsoft standard DI implementation, but this should be avoided to prevent surprises down the road.", count);
			}
			else
			{
				// KEYED SERVICE
				logger.Log(LogLevel.Warning, "FUSION: multiple keyed IFusionCache registrations ({Count}) have been detected with service key {ServiceKey}. The last one will be used, as per Microsoft standard DI implementation, but this should be avoided to prevent surprises down the road.", count, group.Key);
			}
		}

		// CHECK FOR MULTIPLE NAMED CACHES REGISTRATIONS WITH THE SAME NAME
		foreach (var group in serviceProvider.GetServices<LazyNamedCache>().GroupBy(x => x.CacheName))
		{
			var count = group.Count();

			if (count <= 1)
				continue;

			logger.Log(LogLevel.Warning, "FUSION: multiple FusionCache registrations ({Count}) have been detected with cache name {CacheName}. This should be avoided to prevent surprises down the road.", count, group.Key);
		}

	}
}
