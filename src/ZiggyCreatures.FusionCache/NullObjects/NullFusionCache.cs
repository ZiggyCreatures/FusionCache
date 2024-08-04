using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace ZiggyCreatures.Caching.Fusion.NullObjects;

/// <summary>
/// An implementation of <see cref="IFusionCache"/> that implements the null object pattern, meaning that it does nothing. Consider this a kind of a pass-through implementation.
/// </summary>
[DebuggerDisplay("NAME: {_options.CacheName} - ID: {InstanceId}")]
public class NullFusionCache
	: IFusionCache
{
	private readonly FusionCacheOptions _options;
	private readonly FusionCacheEventsHub _events;

	/// <summary>
	/// Creates a new <see cref="NullFusionCache"/> instance.
	/// </summary>
	/// <param name="optionsAccessor">The set of cache-wide options to use with this instance of <see cref="FusionCache"/>.</param>
	public NullFusionCache(IOptions<FusionCacheOptions> optionsAccessor)
	{
		if (optionsAccessor is null)
			throw new ArgumentNullException(nameof(optionsAccessor));

		// OPTIONS
		_options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor.Value));

		// DUPLICATE OPTIONS (TO AVOID EXTERNAL MODIFICATIONS)
		_options = _options.Duplicate();

		// GLOBALLY UNIQUE INSTANCE ID
		InstanceId = _options.InstanceId ?? Guid.NewGuid().ToString("N");

		// EVENTS
		_events = new FusionCacheEventsHub(this, _options, null);
	}

	/// <inheritdoc/>
	public string CacheName
	{
		get { return _options.CacheName; }
	}

	/// <inheritdoc/>
	public string InstanceId { get; }

	/// <inheritdoc/>
	public FusionCacheEntryOptions DefaultEntryOptions
	{
		get { return _options.DefaultEntryOptions; }
	}

	/// <inheritdoc/>
	public bool HasDistributedCache
	{
		get { return false; }
	}

	/// <inheritdoc/>
	public bool HasBackplane
	{
		get { return false; }
	}

	/// <inheritdoc/>
	public FusionCacheEventsHub Events
	{
		get { return _events; }
	}

	/// <inheritdoc/>
	public void AddPlugin(IFusionCachePlugin plugin)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public FusionCacheEntryOptions CreateEntryOptions(Action<FusionCacheEntryOptions>? setupAction = null, TimeSpan? duration = null)
	{
		var res = _options.DefaultEntryOptions.Duplicate(duration);
		setupAction?.Invoke(res);
		return res;
	}

	/// <inheritdoc/>
	public void Expire(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask ExpireAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return new ValueTask();
	}

	/// <inheritdoc/>
	public TValue? GetOrDefault<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return defaultValue;
	}

	/// <inheritdoc/>
	public ValueTask<TValue?> GetOrDefaultAsync<TValue>(string key, TValue? defaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return new ValueTask<TValue?>(defaultValue);
	}

	/// <inheritdoc/>
	public TValue GetOrSet<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return factory(new FusionCacheFactoryExecutionContext<TValue>(options ?? DefaultEntryOptions, default, null, null), token);
	}

	/// <inheritdoc/>
	public TValue GetOrSet<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return defaultValue;
	}

	/// <inheritdoc/>
	public async ValueTask<TValue> GetOrSetAsync<TValue>(string key, Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue>> factory, MaybeValue<TValue> failSafeDefaultValue = default, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return await factory(new FusionCacheFactoryExecutionContext<TValue>(options ?? DefaultEntryOptions, default, null, null), token);
	}

	/// <inheritdoc/>
	public ValueTask<TValue> GetOrSetAsync<TValue>(string key, TValue defaultValue, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return new ValueTask<TValue>(defaultValue);
	}

	/// <inheritdoc/>
	public void Remove(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask RemoveAsync(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return new ValueTask();
	}

	/// <inheritdoc/>
	public IFusionCache RemoveBackplane()
	{
		return this;
	}

	/// <inheritdoc/>
	public IFusionCache SetupSerializer(IFusionCacheSerializer serializer)
	{
		return this;
	}

	/// <inheritdoc/>
	public IFusionCache SetupDistributedCache(IDistributedCache distributedCache)
	{
		return this;
	}

	/// <inheritdoc/>
	public IFusionCache SetupDistributedCache(IDistributedCache distributedCache, IFusionCacheSerializer serializer)
	{
		return this;
	}

	/// <inheritdoc/>
	public IFusionCache RemoveDistributedCache()
	{
		return this;
	}

	/// <inheritdoc/>
	public bool RemovePlugin(IFusionCachePlugin plugin)
	{
		return false;
	}

	/// <inheritdoc/>
	public void Set<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		// EMPTY
	}

	/// <inheritdoc/>
	public ValueTask SetAsync<TValue>(string key, TValue value, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return new ValueTask();
	}

	/// <inheritdoc/>
	public IFusionCache SetupBackplane(IFusionCacheBackplane backplane)
	{
		return this;
	}

	/// <inheritdoc/>
	public MaybeValue<TValue> TryGet<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return default;
	}

	/// <inheritdoc/>
	public ValueTask<MaybeValue<TValue>> TryGetAsync<TValue>(string key, FusionCacheEntryOptions? options = null, CancellationToken token = default)
	{
		return new ValueTask<MaybeValue<TValue>>();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// EMTPY
	}
}
