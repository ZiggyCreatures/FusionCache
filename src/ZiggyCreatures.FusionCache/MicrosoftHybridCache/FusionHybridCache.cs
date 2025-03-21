﻿using System.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;

namespace ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;

/// <summary>
/// An adapter that bridges the world of HybridCache from Microsoft and FusionCache.
/// <br/><br/>
/// <strong>DOCS:</strong> <see href="https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid"/>
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class FusionHybridCache
	: HybridCache
{
	private const FusionCacheEntryOptions? NoEntryOptions = null;

	private readonly IFusionCache _fusionCache;

	/// <summary>
	/// Creates a new <see cref="FusionHybridCache"/> instance that acts as an adapter to the provided <paramref name="fusionCache"/>.
	/// </summary>
	/// <param name="fusionCache"></param>
	public FusionHybridCache(IFusionCache fusionCache)
	{
		_fusionCache = fusionCache;
	}

	/// <summary>
	/// A reference to the inner <see cref="IFusionCache"/> instance.
	/// </summary>
	public IFusionCache InnerFusionCache
	{
		get { return _fusionCache; }
	}

	private FusionCacheEntryOptions? CreateFusionEntryOptions(HybridCacheEntryOptions? options, out bool allowFactory)
	{
		allowFactory = true;

		if (options is null)
		{
			return NoEntryOptions;
		}

		var res = _fusionCache.CreateEntryOptions();

		if (options.Expiration is not null)
		{
			res.DistributedCacheDuration = options.Expiration.Value;
			res.Duration = options.LocalCacheExpiration ?? options.Expiration.Value;
		}

		if (options.Flags is not null && options.Flags.Value != HybridCacheEntryFlags.None)
		{
			var flags = options.Flags.Value;

			allowFactory = flags.HasFlag(HybridCacheEntryFlags.DisableUnderlyingData) == false;

			res.SkipMemoryCacheRead = flags.HasFlag(HybridCacheEntryFlags.DisableLocalCacheRead);
			res.SkipMemoryCacheWrite = flags.HasFlag(HybridCacheEntryFlags.DisableLocalCacheWrite);

			res.SkipDistributedCacheRead = flags.HasFlag(HybridCacheEntryFlags.DisableDistributedCacheRead);
			res.SkipDistributedCacheWrite = flags.HasFlag(HybridCacheEntryFlags.DisableDistributedCacheWrite);
		}

		return res;
	}

	/// <inheritdoc/>
	public override async ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory, HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
	{
		var feo = CreateFusionEntryOptions(options, out var allowFactory);

		if (allowFactory == false)
		{
			// GET ONLY (NO FACTORY EXECUTION)
			//
			// NOTE: I'M FORCED TO FORCE THE RETURN VALUE TO APPEAR NOT TO BE null BECAUSE THAT IS
			// HOW THE PUBLIC API SURFACE AREA OF HybridCache IS DESIGNED.
			// WHEN DISABLING THE FACTORY (EG: WHEN USING HybridCacheEntryFlags.DisableUnderlyingData)
			// THE METHOD CALL GetOrCreateAsync TURNS INTO A GET-ONLY METHOD: THAT, IN TURN, WOULD
			// RETURN THE DEFAULT VALUE IF NOTHING IS IN THE CACHE, WHICH IN TURN CAN BE null FOR
			// REFERENCE TYPES.
			// BY HAVING A SINGLE METHOD TO EXPRESS BOTH THE GET-ONLY AND GET-SET METHODS, IT MAKES
			// THE RETURN TYPE SIGNATURE THEREFORE TECHNICALLY "WRONG", BUT THERE'S NOTHING I CAN DO
			// ABOUT IT, AND RETURNING null IN THOSE CASES IS ANYWAY THE EXPECTED BEHAVIOUR FOR
			// HybridCache USERS.
			return (await _fusionCache.GetOrDefaultAsync<T>(key, default, feo, cancellationToken))!;
		}

		// GET + SET
		return await _fusionCache.GetOrSetAsync<T>(key, async (_, ct) => await factory(state, ct), default, feo, tags, cancellationToken);
	}

	/// <inheritdoc/>
	public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
	{
		return _fusionCache.RemoveAsync(key, NoEntryOptions, cancellationToken);
	}

	/// <inheritdoc/>
	public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
	{
		return _fusionCache.RemoveByTagAsync(tag, NoEntryOptions, cancellationToken);
	}

	/// <inheritdoc/>
	public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
	{
		return _fusionCache.SetAsync(key, value, CreateFusionEntryOptions(options, out _), tags, cancellationToken);
	}

	private string GetDebuggerDisplay()
	{
		return $"HybridCache Adapter for FusionCache with name {InnerFusionCache.CacheName}";
	}
}
