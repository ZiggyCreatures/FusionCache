using FusionCacheTests.Stuff;
using Xunit;

namespace FusionCacheTests;

public partial class EventsTests
	: AbstractTests
{
	public EventsTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	private readonly TimeSpan InitialBackplaneDelay = TimeSpan.FromMilliseconds(300);
}
