using FusionCacheTests.Stuff;
using Xunit;

namespace FusionCacheTests;

public partial class L1Tests
	: AbstractTests
{
	public L1Tests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private static string CreateRandomCacheKey(string key)
	{
		return key + "_" + Guid.NewGuid().ToString("N");
	}
}
