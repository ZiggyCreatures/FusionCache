using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests.Stuff;

internal class SimpleDefaultEntryOptionsProvider
	: FusionCacheEntryOptionsProvider
{
	private readonly ILogger<SimpleDefaultEntryOptionsProvider> _logger;

	public SimpleDefaultEntryOptionsProvider(ILogger<SimpleDefaultEntryOptionsProvider> logger)
	{
		_logger = logger;
	}

	public override FusionCacheEntryOptions? GetEntryOptions(FusionCacheEntryOptionsProviderContext ctx, string key, out bool canMutate)
	{
		_logger.LogInformation("-> KEY: {Key}", key);

		canMutate = false;
		return null;
	}
}
