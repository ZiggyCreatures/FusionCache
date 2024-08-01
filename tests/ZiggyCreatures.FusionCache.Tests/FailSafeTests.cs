using System;
using System.Threading.Tasks;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

public class FailSafeTests
{
	private readonly FusionCache _cache;
	private readonly string _key;

	public FailSafeTests()
	{
		var options = new FusionCacheOptions();

		options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(5);
		options.DefaultEntryOptions.IsFailSafeEnabled = true; // enable by default

		_cache = new FusionCache(options);
		_key = "foo";

		_cache.GetOrSet(_key, "bar"); // init the key

		_cache.Expire(_key); // logically expire the key so the fail safe logic triggers.
	}

	[Fact]
	public async Task FailSafe_CanBeDisabled_OnFactoryFailureAsync()
	{
		await Assert.ThrowsAsync<Exception>(async () =>
		{
			await _cache.GetOrSetAsync<string>(_key, (ctx, ct) =>
			{
				try
				{
					throw new Exception("Factory failed");
				}
				finally
				{
					ctx.Options.SetFailSafe(false); // disable fail safe.
				}
			});
		});
	}

	[Fact]
	public async Task FailSafe_CanBeDisabled_OnFactoryFailure()
	{
		Assert.Throws<Exception>(() =>
		{
			_cache.GetOrSet<string>(_key, (ctx, ct) =>
			{
				try
				{
					throw new Exception("Factory failed");
				}
				finally
				{
					ctx.Options.SetFailSafe(false); // disable fail safe.
				}
			});
		});
	}
}

