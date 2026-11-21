using FusionCacheTests.Stuff;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

/// <summary>
/// Tests for the buffered L2 path, which is used only when the distributed cache implements <see cref="Microsoft.Extensions.Caching.Distributed.IBufferDistributedCache"/> AND the serializer implements <see cref="ZiggyCreatures.Caching.Fusion.Serialization.IBufferFusionCacheSerializer"/>: in every other combination the classic (byte[]) path must be used, producing the exact same bytes.
/// </summary>
public partial class BufferedL2Tests
	: AbstractTests
{
	public BufferedL2Tests(ITestOutputHelper output)
		: base(output, "MyCache:")
	{
	}

	private const string SampleString = "Supercalifragilisticexpialidocious";

	private static IDistributedCache CreateInnerDistributedCache()
	{
		return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}

	private FusionCacheOptions CreateFusionCacheOptions()
	{
		return new FusionCacheOptions
		{
			CacheKeyPrefix = TestingCacheKeyPrefix,
			DefaultEntryOptions = new FusionCacheEntryOptions
			{
				Duration = TimeSpan.FromMinutes(10),
				// SURFACE ANY L2/SERIALIZATION PROBLEM INSTEAD OF SILENTLY DEGRADING
				ReThrowDistributedCacheExceptions = true,
				ReThrowSerializationExceptions = true,
			}
		};
	}

	private static string CreateRandomCacheKey(string key)
	{
		return key + "_" + Guid.NewGuid().ToString("N");
	}
}
