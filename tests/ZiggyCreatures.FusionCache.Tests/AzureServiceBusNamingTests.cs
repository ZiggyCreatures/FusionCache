using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

namespace FusionCacheTests;

public class AzureServiceBusNamingTests
	: AbstractTests
{
	public AzureServiceBusNamingTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	[Fact]
	public void SanitizeEntityNameThrowsWhenNameIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => AzureServiceBusNaming.SanitizeEntityName(null!, 50));
	}

	[Fact]
	public void SanitizeEntityNameLeavesValidCharactersUntouched()
	{
		var result = AzureServiceBusNaming.SanitizeEntityName("My.Cache-Name_v1/sub", 260);

		Assert.Equal("My.Cache-Name_v1/sub", result);
	}

	[Fact]
	public void SanitizeEntityNameReplacesInvalidCharactersWithDashes()
	{
		// ':' AND ' ' ARE NOT VALID SERVICE BUS ENTITY NAME CHARACTERS
		var result = AzureServiceBusNaming.SanitizeEntityName("MyCache.Backplane:v1", 260);

		Assert.Equal("MyCache.Backplane-v1", result);
		Assert.DoesNotContain(':', result);
	}

	[Fact]
	public void SanitizeEntityNameTrimsLeadingAndTrailingSeparators()
	{
		var result = AzureServiceBusNaming.SanitizeEntityName("///cache-name---", 260);

		Assert.Equal("cache-name", result);
	}

	[Fact]
	public void SanitizeEntityNameTruncatesToMaxLength()
	{
		var longName = new string('a', 300);

		var result = AzureServiceBusNaming.SanitizeEntityName(longName, 50);

		Assert.Equal(50, result.Length);
	}

	[Fact]
	public void SanitizeEntityNameReturnsFallbackWhenResultWouldBeEmpty()
	{
		var result = AzureServiceBusNaming.SanitizeEntityName("::: ***", 50, fallback: "my-fallback");

		Assert.Equal("my-fallback", result);
	}

	[Fact]
	public void ResolveTopicNameUsesExplicitTopicNameWhenProvided()
	{
		var result = AzureServiceBusNaming.ResolveTopicName("my-explicit-topic", "MyCache.Backplane:v1");

		Assert.Equal("my-explicit-topic", result);
	}

	[Fact]
	public void ResolveTopicNameFallsBackToChannelNameWhenNotProvided()
	{
		// THIS IS FUSIONCACHE'S DEFAULT COMPUTED CHANNEL NAME SHAPE (SEE FusionCacheInternalUtils.GetBackplaneChannelName):
		// THE ':' SEPARATOR IS NOT A VALID SERVICE BUS CHARACTER, SO IT MUST BE SANITIZED AWAY
		var result = AzureServiceBusNaming.ResolveTopicName(null, "MyCache.Backplane:v1");

		Assert.Equal("MyCache.Backplane-v1", result);
	}

	[Fact]
	public void ResolveTopicNameFallsBackToChannelNameWhenExplicitTopicNameIsWhitespace()
	{
		var result = AzureServiceBusNaming.ResolveTopicName("   ", "MyCache.Backplane:v1");

		Assert.Equal("MyCache.Backplane-v1", result);
	}
}
