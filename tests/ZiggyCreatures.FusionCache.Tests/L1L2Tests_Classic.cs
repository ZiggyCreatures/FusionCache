using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace FusionCacheTests;

public sealed class L1L2Tests_Classic(ITestOutputHelper output) : L1L2Tests(output)
{
	protected override IDistributedCache CreateDistributedCache()
	{
		return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}
}
