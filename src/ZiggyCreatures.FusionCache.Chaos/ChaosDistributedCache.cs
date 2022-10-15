using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace ZiggyCreatures.Caching.Fusion.Chaos
{
	/// <summary>
	/// An implementation of <see cref="IDistributedCache"/> that acts on behalf of another one, but with a (controllable) amount of chaos in-between.
	/// </summary>
	public class ChaosDistributedCache
		: IDistributedCache
	{
		private readonly IDistributedCache _innerCache;

		/// <summary>
		/// Initializes a new instance of the ChaosDistributedCache class.
		/// </summary>
		/// <param name="innerCache">The actual <see cref="IDistributedCache"/> used if and when chaos does not happen.</param>
		public ChaosDistributedCache(IDistributedCache innerCache)
		{
			_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
			SetNeverChaos();
		}

		/// <summary>
		/// A <see cref="float"/> value from 0.0 to 1.0 that represents the probabilty of throwing an exception: set it to 0.0 to never throw or to 1.0 to always throw.
		/// </summary>
		public float ChaosThrowProbability { get; set; }

		/// <summary>
		/// The minimum amount of randomized delay.
		/// </summary>
		public TimeSpan ChaosMinDelay { get; set; }

		/// <summary>
		/// The maximum amount of randomized delay.
		/// </summary>
		public TimeSpan ChaosMaxDelay { get; set; }

		/// <summary>
		/// Force chaos exceptions to never be thrown.
		/// </summary>
		public void SetNeverThrow()
		{
			ChaosThrowProbability = 0f;
		}

		/// <summary>
		/// Force chaos exceptions to always be thrown.
		/// </summary>
		public void SetAlwaysThrow()
		{
			ChaosThrowProbability = 1f;
		}

		/// <summary>
		/// Force chaos delays to never happen.
		/// </summary>
		public void SetNeverDelay()
		{
			ChaosMinDelay = TimeSpan.Zero;
			ChaosMaxDelay = TimeSpan.Zero;
		}

		/// <summary>
		/// Force chaos delays to always be of exactly this amount.
		/// </summary>
		/// <param name="delay"></param>
		public void SetAlwaysDelayExactly(TimeSpan delay)
		{
			ChaosMinDelay = delay;
			ChaosMaxDelay = delay;
		}

		/// <summary>
		/// Force chaos exceptions and delays to never happen.
		/// </summary>
		public void SetNeverChaos()
		{
			SetNeverThrow();
			SetNeverDelay();
		}

		/// <summary>
		/// Force chaos exceptions to always throw, and chaos delays to always be of exactly this amount.
		/// </summary>
		/// <param name="delay"></param>
		public void SetAlwaysChaos(TimeSpan delay)
		{
			SetAlwaysThrow();
			SetAlwaysDelayExactly(delay);
		}

		/// <inheritdoc/>
		public byte[] Get(string key)
		{
			FusioCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			return _innerCache.Get(key);
		}

		/// <inheritdoc/>
		public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
		{
			await FusioCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability).ConfigureAwait(false);
			return await _innerCache.GetAsync(key, token).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Refresh(string key)
		{
			FusioCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			_innerCache.Refresh(key);
		}

		/// <inheritdoc/>
		public async Task RefreshAsync(string key, CancellationToken token = default)
		{
			await FusioCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability).ConfigureAwait(false);
			await _innerCache.RefreshAsync(key, token).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Remove(string key)
		{
			FusioCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			_innerCache.Remove(key);
		}

		/// <inheritdoc/>
		public async Task RemoveAsync(string key, CancellationToken token = default)
		{
			await FusioCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability).ConfigureAwait(false);
			await _innerCache.RemoveAsync(key, token).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			FusioCacheChaosUtils.MaybeChaos(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability);
			_innerCache.Set(key, value, options);
		}

		/// <inheritdoc/>
		public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
		{
			await FusioCacheChaosUtils.MaybeChaosAsync(ChaosMinDelay, ChaosMaxDelay, ChaosThrowProbability).ConfigureAwait(false);
			await _innerCache.SetAsync(key, value, options, token).ConfigureAwait(false);
		}
	}
}
