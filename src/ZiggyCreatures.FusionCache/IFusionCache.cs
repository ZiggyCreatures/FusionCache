using System;
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

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	ValueTask<TValue?> GetOrSetAsync<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue?>> factory, MaybeValue<TValue?> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="factory"/> will be called and the returned value saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="factory">The function which will be called if the value is not found in the cache.</param>
	/// <param name="failSafeDefaultValue">In case fail-safe is activated and there's no stale data to use, this value will be used instead of throwing an exception.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>The value in the cache, either already there or generated using the provided <paramref name="factory"/> .</returns>
	TValue? GetOrSet<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue?> factory, MaybeValue<TValue?> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	ValueTask<TValue?> GetOrSetAsync<TValue>(string key, TValue? defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Get the value of type <typeparamref name="TValue"/> in the cache for the specified <paramref name="key"/>: if not there, the <paramref name="defaultValue"/> will be saved according to the <paramref name="options"/> provided.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="defaultValue">In case the value is not in the cache this value will be saved and returned instead.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	TValue? GetOrSet<TValue>(string key, TValue? defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default);

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

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="options"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Put the <paramref name="value"/> in the cache for the specified <paramref name="key"/> with the provided <paramref name="options"/>. If a value is already there it will be overwritten.
	/// </summary>
	/// <typeparam name="TValue">The type of the value in the cache.</typeparam>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="value">The value to put in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default);

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

	/// <summary>
	/// Expires the cache entry for the specified <paramref name="key"/>.
	/// <br/>
	/// <br/>
	/// In the memory cache:
	/// <br/>
	/// - if fail-safe is enabled: the entry will marked as logically expired, but still be available as a fallback value in case of problems
	/// <br/>
	/// - if fail-safe is disabled: the entry will be effectively removed
	/// <br/>
	/// <br/>
	/// In the distributed cache (if any), the entry will be effectively removed.
	/// </summary>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	/// <returns>A <see cref="ValueTask"/> to await the completion of the operation.</returns>
	ValueTask ExpireAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Expires the cache entry for the specified <paramref name="key"/>.
	/// <br/>
	/// <br/>
	/// In the memory cache:
	/// <br/>
	/// - if fail-safe is enabled: the entry will marked as logically expired, but still be available as a fallback value in case of problems
	/// <br/>
	/// - if fail-safe is disabled: the entry will be effectively removed
	/// <br/>
	/// <br/>
	/// In the distributed cache (if any), the entry will be effectively removed.
	/// </summary>
	/// <param name="key">The cache key which identifies the entry in the cache.</param>
	/// <param name="options">The options to adhere during this operation. If null is passed, <see cref="DefaultEntryOptions"/> will be used.</param>
	/// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	void Expire(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// Sets a secondary caching layer, by providing an <see cref="IDistributedCache"/> instance and an <see cref="IFusionCacheSerializer"/> instance to be used to convert from generic values to byte[] and viceversa.
	/// </summary>
	/// <param name="distributedCache">The <see cref="IDistributedCache"/> instance to use.</param>
	/// <param name="serializer">The <see cref="IFusionCacheSerializer"/> instance to use.</param>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache SetupDistributedCache(IDistributedCache distributedCache, IFusionCacheSerializer serializer);

	/// <summary>
	/// Removes the secondary caching layer.
	/// </summary>
	/// <returns>The same <see cref="IFusionCache"/> instance, usable in a fluent api way.</returns>
	IFusionCache RemoveDistributedCache();

	/// <summary>
	/// Gets whether there is a distributed cache configured.
	/// </summary>
	bool HasDistributedCache { get; }

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

	///// <summary>
	///// Tries to send a message to other nodes connected to the same backplane, if any.
	///// </summary>
	///// <param name="message">The message to send. It can be created using one of the static methods like BackplaneMessage.CreateForXyz().</param>
	///// <param name="options">The options to use.</param>
	///// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	///// <returns><see langword="true"/> if there was at least one backplane to send a notification to, otherwise <see langword="false"/>.</returns>
	//ValueTask<bool> PublishAsync(BackplaneMessage message, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	///// <summary>
	///// Tries to send a message to other nodes connected to the same backplane, if any.
	///// </summary>
	///// <param name="message">The message to send. It can be created using one of the static methods like BackplaneMessage.CreateForXyz().</param>
	///// <param name="options">The options to use.</param>
	///// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
	///// <returns><see langword="true"/> if there was at least one backplane to send a notification to, otherwise <see langword="false"/>.</returns>
	//bool Publish(BackplaneMessage message, FusionCacheEntryOptions? options = null, CancellationToken token = default);

	/// <summary>
	/// The central place for all events handling of this FusionCache instance.
	/// </summary>
	FusionCacheEventsHub Events { get; }

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
