using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// The shared interface that models what a FusionCache instance can do.
/// </summary>
public interface IFusionCache
	: IDisposable
{
	/// <summary>
	/// The name of the cache: it can be used for identification, and in a multi-node scenario it is typically shared between nodes to create a logical association.
	/// </summary>
	string CacheName { get; }

	/// <summary>
	/// A globally unique Id, auto-generated when a new instance is created (eg: the ctor is called), representing this specific cache instance: this will change every time a FusionCache instance is created.
	/// </summary>
	string InstanceId { get; }

	/// <summary>
	/// The default set of options that will be used either when none are provided or as a starting point for creating a new one with the fluent api.
	/// </summary>
	FusionCacheEntryOptions DefaultEntryOptions { get; }

	/// <summary>
	/// Creates a new <see cref="FusionCacheEntryOptions"/> instance by duplicating the <see cref="DefaultEntryOptions"/> and optionally applying a setup action.
	/// </summary>
	/// <param name="setupAction">An optional setup action to further configure the newly created <see cref="FusionCacheEntryOptions"/> instance.</param>
	/// <param name="duration">An optional duration to directly change the <see cref="FusionCacheEntryOptions.Duration"/> of the newly created <see cref="FusionCacheEntryOptions"/> instance.</param>
	/// <returns>The newly created <see cref="FusionCacheEntryOptions"/>.</returns>
	FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null);

	// GET OR SET

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	ValueTask<TValue> GetOrSetAsync<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	TValue GetOrSet<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	ValueTask<TValue> GetOrSetAsync<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	TValue GetOrSet<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default);

	// GET OR DEFAULT

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be returned.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">The default value to return if the value for the given <paramref name="key"/> is not in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache or the <paramref name="defaultValue"/> .</returns>
	ValueTask<TValue?> GetOrDefaultAsync<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be returned.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">The default value to return if the value for the given <paramref name="key"/> is not in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache or the <paramref name="defaultValue"/> .</returns>
	TValue? GetOrDefault<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	// TRY GET

	/// <summary>
	/// Try to get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/> and returns a <see cref="MaybeValue{TValue}"/> instance.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	ValueTask<MaybeValue<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Try to get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/> and returns a <see cref="MaybeValue{TValue}"/> instance.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	// SET

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/>, optionally tagged with the specified <paramref name="tags"/>, with the provided <paramref name="options"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default);

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="options"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="tags">The optional set of tags related to the entry: this may be used to remove/expire multiple entries at once, by tag.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken token = default);

	// REMOVE

	/// <summary>
	/// Removes the value in the cache for the specified <paramref name="key"/>.
	/// </summary>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask RemoveAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Removes the value in the cache for the specified <paramref name="key"/>.
	/// </summary>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Remove(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	// EXPIRE

	/// <summary>
	/// Expires the cache entry for the specified <paramref name="key"/>: that can mean an Expire (if fail-safe was enabled when saving the entry) or a Remove (if fail-safe was NOT enabled when saving the entry), all automatically.
	/// <br/>
	/// <br/>
	/// In the distributed cache (if any), the entry will always be effectively removed.
	/// </summary>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask ExpireAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Expires the cache entry for the specified <paramref name="key"/>: that can mean an Expire (if fail-safe was enabled when saving the entry) or a Remove (if fail-safe was NOT enabled when saving the entry), all automatically.
	/// <br/>
	/// <br/>
	/// In the distributed cache (if any), the entry will always be effectively removed.
	/// </summary>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Expire(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	// TAGGING

	/// <summary>
	/// Remove all entries tagged with the specified <paramref name="tag"/>: for each entry, that can mean an Expire (if fail-safe was enabled when saving the entry) or a Remove (if fail-safe was NOT enabled when saving the entry), all automatically.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	/// <param name="tag">The tag to use to identify the entries to remove.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="FusionCacheOptions.TagsDefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask RemoveByTagAsync(string tag, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Expire all entries tagged with the specified <paramref name="tag"/>: for each entry, that can mean an Expire (if fail-safe was enabled when saving the entry) or a Remove (if fail-safe was NOT enabled when saving the entry), all automatically.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// </summary>
	/// <param name="tag">The tag to use to identify the entries to remove.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="FusionCacheOptions.TagsDefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void RemoveByTag(string tag, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	// CLEAR

	/// <summary>
	/// Expire or remove all entries in the cache, based on the <paramref name="allowFailSafe"/> param.
	/// <br/>
	/// This works in all scenarios, including L1-only (memory level), L1+L2 (memory level + distributed level), shared caches, with cache key prefix, etc.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Clear.md"/>
	/// </summary>
	/// <param name="allowFailSafe">If set to <see langword="true"/> it will expire all entries in the cache, if set to <see langword="false"/> it will remove all entries in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask ClearAsync(bool allowFailSafe = true, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Expire or remove all entries in the cache, based on the <paramref name="allowFailSafe"/> param.
	/// <br/>
	/// This works in all scenarios, including L1-only (memory level), L1+L2 (memory level + distributed level), shared caches, with cache key prefix, etc.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md"/>
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Clear.md"/>
	/// </summary>
	/// <param name="allowFailSafe">If set to <see langword="true"/> it will expire all entries in the cache, if set to <see langword="false"/> it will remove all entries in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Clear(bool allowFailSafe = true, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	// SERIALIZATION

	/// <summary>
	/// Setup a serializer by providing an <see cref="IFusionCacheSerializer"/> instance to be used with the distributed level (L2) if also setup, or for auto-clone.
	/// </summary>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use.</param>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache SetupSerializer(IFusionCacheSerializer serializer);

	// DISTRIBUTED CACHE

	/// <summary>
	/// Setup a distributed level (L2), by providing an <see cref="IDistributedCache"/>.
	/// <br/>
	/// Please note that a serializer (instance of <see cref="IFusionCacheSerializer"/>) must be setup before calling this method), otherwise use the overload that accepts them both.
	/// </summary>
	/// <param name="distributedCache">The <see cref="IDistributedCache"/> instance to use.</param>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache SetupDistributedCache(IDistributedCache distributedCache);

	/// <summary>
	/// Setup a secondary caching level, by providing an <see cref="IDistributedCache"/> instance and an <see cref="IFusionCacheSerializer"/> instance to be used to convert from generic values to byte[] and vice versa.
	/// </summary>
	/// <param name="distributedCache">The <see cref="IDistributedCache"/> instance to use.</param>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use.</param>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache SetupDistributedCache(IDistributedCache distributedCache, IFusionCacheSerializer serializer);

	/// <summary>
	/// Removes the secondary caching level.
	/// </summary>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache RemoveDistributedCache();

	/// <summary>
	/// Gets whether there is a distributed cache configured.
	/// </summary>
	bool HasDistributedCache { get; }

	// BACKPLANE

	/// <summary>
	/// Sets a backplane, by providing an <see cref="IFusionCacheBackplane"/> instance.
	/// </summary>
	/// <param name="backplane">The <see cref="IFusionCacheBackplane"/> instance to use.</param>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache SetupBackplane(IFusionCacheBackplane backplane);

	/// <summary>
	/// Removes the backplane.
	/// </summary>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache RemoveBackplane();

	/// <summary>
	/// Gets whether there is a backplane configured.
	/// </summary>
	bool HasBackplane { get; }

	// EVENTS

	/// <summary>
	/// The central place for all events handling of this FusionCache instance.
	/// </summary>
	FusionCacheEventsHub Events { get; }

	// PLUGINS

	/// <summary>
	/// Add a plugin to this FusionCache instance, then start it.
	/// </summary>
	/// <param name="plugin">The <see cref="IFusionCachePlugin"/> instance.</param>
	void AddPlugin(IFusionCachePlugin plugin);

	/// <summary>
	/// Stop a plugin, then remove it from this FusionCache instance.
	/// </summary>
	/// <param name="plugin">The <see cref="IFusionCachePlugin"/> instance.</param>
	/// <returns><see langword="true"/> if the plugin has been removed, otherwise <see langword="false"/>.</returns>
	bool RemovePlugin(IFusionCachePlugin plugin);
}
