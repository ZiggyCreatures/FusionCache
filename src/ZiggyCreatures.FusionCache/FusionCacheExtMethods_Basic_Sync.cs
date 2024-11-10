using System;
using System.Collections.Generic;
using System.Threading;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion;

public static partial class FusionCacheExtMethods
{
	#region GetOrSet overloads (with default value)

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according with the <paramref name="duration"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static TValue GetOrSet<TValue>(this IFusionCache cache, string key, TValue defaultValue, TimeSpan duration, CancellationToken token)
	{
		return cache.GetOrSet<TValue>(key, defaultValue, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according with the <paramref name="duration"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static TValue GetOrSet<TValue>(this IFusionCache cache, string key, TValue defaultValue, TimeSpan duration, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSet<TValue>(key, defaultValue, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static TValue GetOrSet<TValue>(this IFusionCache cache, string key, TValue defaultValue, Action<FusionCacheEntryOptions> setupAction, CancellationToken token)
	{
		return cache.GetOrSet<TValue>(key, defaultValue, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static TValue GetOrSet<TValue>(this IFusionCache cache, string key, TValue defaultValue, Action<FusionCacheEntryOptions> setupAction, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		return cache.GetOrSet<TValue>(key, defaultValue, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	#endregion

	#region GetOrDefault overloads

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be returned.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">The default value to return if the value for the given <paramref name="key"/> is not in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache or the <paramref name="defaultValue"/> .</returns>
	public static TValue? GetOrDefault<TValue>(this IFusionCache cache, string key, Action<FusionCacheEntryOptions> setupAction, TValue? defaultValue = default, CancellationToken token = default)
	{
		return cache.GetOrDefault<TValue>(key, defaultValue, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), token);
	}

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be returned.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">The default value to return if the value for the given <paramref name="key"/> is not in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache or the <paramref name="defaultValue"/> .</returns>
	public static TValue? GetOrDefault<TValue>(this IFusionCache cache, string key, TValue? defaultValue, Action<FusionCacheEntryOptions> setupAction, CancellationToken token = default)
	{
		return cache.GetOrDefault<TValue>(key, defaultValue, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), token);
	}

	#endregion

	#region TryGet overloads

	/// <summary>
	/// Try to get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/> and returns a <see cref="MaybeValue{TValue}"/> instance.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static MaybeValue<TValue> TryGet<TValue>(this IFusionCache cache, string key, Action<FusionCacheEntryOptions> setupAction, CancellationToken token = default)
	{
		return cache.TryGet<TValue>(key, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), token);
	}

	#endregion

	#region Set overloads

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="duration"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Set<TValue>(this IFusionCache cache, string key, TValue value, TimeSpan duration, CancellationToken token)
	{
		cache.Set<TValue>(key, value, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="duration"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="duration">The value for the newly created <see cref="FusionCacheEntryOptions.Duration"/> property, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Set<TValue>(this IFusionCache cache, string key, TValue value, TimeSpan duration, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		cache.Set<TValue>(key, value, cache.DefaultEntryOptions.Duplicate(duration).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Set<TValue>(this IFusionCache cache, string key, TValue value, Action<FusionCacheEntryOptions> setupAction, CancellationToken token)
	{
		cache.Set<TValue>(key, value, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), FusionCacheInternalUtils.NoTags, token);
	}

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the <see cref="FusionCacheEntryOptions"/> resulting by calling the provided <paramref name="setupAction"/> lambda. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Set<TValue>(this IFusionCache cache, string key, TValue value, Action<FusionCacheEntryOptions> setupAction, IEnumerable<string>? tags = null, CancellationToken token = default)
	{
		cache.Set<TValue>(key, value, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), tags, token);
	}

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="options"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="IFusionCache.DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Set<TValue>(this IFusionCache cache, string key, TValue value, FusionCacheEntryOptions? options, CancellationToken token)
	{
		cache.Set<TValue>(key, value, options, FusionCacheInternalUtils.NoTags, token);
	}

	#endregion

	#region Remove overloads

	/// <summary>
	/// Removes the value in the cache for the specified <paramref name="key"/>.
	/// </summary>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Remove(this IFusionCache cache, string key, Action<FusionCacheEntryOptions> setupAction, CancellationToken token = default)
	{
		cache.Remove(key, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), token);
	}

	#endregion

	#region Expire overloads

	/// <summary>
	/// Expires the cache entry for the specified <paramref name="key"/>.
	/// <br/>
	/// <br/>
	/// In the memory cache:
	/// <br/>
	/// - if fail-safe is enabled: the entry will marked as logically expired, but will still be available as a fallback value in case of future problems
	/// <br/>
	/// - if fail-safe is disabled: the entry will be effectively removed
	/// <br/>
	/// <br/>
	/// In the distributed cache (if any), the entry will be effectively removed.
	/// </summary>
	/// <param name="cache">The <see cref="IFusionCache"/> instance.</param>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="setupAction">The setup action used to further configure the newly created <see cref="FusionCacheEntryOptions"/> object, automatically created by duplicating <see cref="IFusionCache.DefaultEntryOptions"/>.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	public static void Expire(this IFusionCache cache, string key, Action<FusionCacheEntryOptions> setupAction, CancellationToken token = default)
	{
		cache.Expire(key, cache.CreateEntryOptions(setupAction).SetIsSafeForAdaptiveCaching(), token);
	}

	#endregion
}
