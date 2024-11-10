using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion;

public static partial class FusionCacheExtMethods
{
	#region GetOrSet overloads (with factory and fail-safe default value)

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="IFusionCache.DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions? options, CancellationToken token)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), failSafeDefaultValue, options, FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="IFusionCache.DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), failSafeDefaultValue, options, tags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="duration"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue, TimeSpan duration, CancellationToken token)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), failSafeDefaultValue, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="duration"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue, TimeSpan duration, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), failSafeDefaultValue, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue, Action<FusionCacheEntryOptions> setupAction, CancellationToken token)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), failSafeDefaultValue, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue, Action<FusionCacheEntryOptions> setupAction, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), failSafeDefaultValue, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	#endregion

	#region GetOrSet overloads (with factory)

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="IFusionCache.DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, FusionCacheEntryOptions? options, CancellationToken token)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), default, options, FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="IFusionCache.DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), default, options, tags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="duration"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, TimeSpan duration, CancellationToken token)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), default, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <paramref name="duration"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, TimeSpan duration, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), default, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, Action<FusionCacheEntryOptions> setupAction, CancellationToken token)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), default, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	public static ValueTask<TValue> GetOrSetAsync<TValue>(this IFusionCache cache, string key, Func<CancellationToken, Task<TValue>> factory, Action<FusionCacheEntryOptions> setupAction, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSetAsync<TValue>(key, (_, ct) => factory(ct), default, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	#endregion
}
