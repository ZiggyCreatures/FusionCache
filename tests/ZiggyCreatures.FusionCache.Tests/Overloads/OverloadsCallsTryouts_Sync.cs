using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests.Overloads;

internal static partial class OverloadsCallsTryouts
{
	internal static void GetOrSetCalls(IFusionCache cache)
	{
		// FACTORY / FAIL-SAFE DEFAULT VALUE
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			DefaultValue,
			Duration
		);
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			DefaultValue,
			OptionsLambda
		);
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			DefaultValue,
			Options
		);
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			DefaultValue
		);

		// FACTORY / NO FAIL-SAFE DEFAULT VALUE
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			Duration
		);
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			OptionsLambda
		);
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory,
			Options
		);
		_ = cache.GetOrSet<int?>(
			Key,
			SyncFactory
		);

		// NO FACTORY / FAIL-SAFE DEFAULT VALUE
		_ = cache.GetOrSet<int?>(
			Key,
			DefaultValue,
			Duration
		);
		_ = cache.GetOrSet<int?>(
			Key,
			DefaultValue,
			OptionsLambda
		);
		_ = cache.GetOrSet<int?>(
			Key,
			DefaultValue,
			Options
		);
		_ = cache.GetOrSet<int?>(
			Key,
			DefaultValue
		);
	}

	internal static void GetOrDefaultCalls(IFusionCache cache)
	{
		_ = cache.GetOrDefault<int?>(
			Key,
			DefaultValue,
			OptionsLambda
		);
		_ = cache.GetOrDefault<int?>(
			Key,
			DefaultValue,
			Options
		);
		_ = cache.GetOrDefault<int?>(
			Key,
			DefaultValue
		);
	}

	internal static void SetCalls(IFusionCache cache)
	{
		cache.Set<int?>(
			Key,
			DefaultValue,
			OptionsLambda
		);
		cache.Set<int?>(
			Key,
			DefaultValue,
			Options
		);
		cache.Set<int?>(
			Key,
			DefaultValue
		);
	}

	internal static void RemoveCalls(IFusionCache cache)
	{
		cache.Remove(
			Key,
			OptionsLambda
		);
		cache.Remove(
			Key,
			Options
		);
	}
}
