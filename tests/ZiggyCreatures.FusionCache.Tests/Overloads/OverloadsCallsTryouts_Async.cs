using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests.Overloads;

internal static partial class OverloadsCallsTryouts
{
	internal static async Task GetOrSetCallsAsync(IFusionCache cache)
	{
		// FACTORY / FAIL-SAFE DEFAULT VALUE
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			DefaultValue,
			Duration
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			DefaultValue,
			OptionsLambda
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			DefaultValue,
			Options
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			DefaultValue
		);

		// FACTORY / NO FAIL-SAFE DEFAULT VALUE
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			Duration
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			OptionsLambda
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory,
			Options
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			AsyncFactory
		);

		// NO FACTORY / FAIL-SAFE DEFAULT VALUE
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			DefaultValue,
			Duration
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			DefaultValue,
			OptionsLambda
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			DefaultValue,
			Options
		);
		_ = await cache.GetOrSetAsync<int?>(
			Key,
			DefaultValue
		);
	}

	internal static async Task GetOrDefaultCallsAsync(IFusionCache cache)
	{
		_ = await cache.GetOrDefaultAsync<int?>(
			Key,
			DefaultValue,
			OptionsLambda
		);
		_ = await cache.GetOrDefaultAsync<int?>(
			Key,
			DefaultValue,
			Options
		);
		_ = await cache.GetOrDefaultAsync<int?>(
			Key,
			DefaultValue
		);
	}

	internal static async Task SetCallsAsync(IFusionCache cache)
	{
		await cache.SetAsync<int?>(
			Key,
			DefaultValue,
			OptionsLambda
		);
		await cache.SetAsync<int?>(
			Key,
			DefaultValue,
			Options
		);
		await cache.SetAsync<int?>(
			Key,
			DefaultValue
		);
	}

	internal static async Task RemoveCallsAsync(IFusionCache cache)
	{
		await cache.RemoveAsync(
			Key,
			OptionsLambda
		);
		await cache.RemoveAsync(
			Key,
			Options
		);
	}
}
