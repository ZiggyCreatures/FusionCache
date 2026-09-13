using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

namespace FusionCacheTests.AzureServiceBus;

public class AzureServiceBusHelpersTests
	: AbstractTests
{
	public AzureServiceBusHelpersTests(ITestOutputHelper output)
		: base(output, null)
	{
	}

	[Fact]
	public void SanitizeEntityNameLeavesValidCharactersUntouched()
	{
		var result = AzureServiceBusHelpers.SanitizeEntityName("My.Cache-Name_v1/sub", 260);

		Assert.Equal("My.Cache-Name_v1/sub", result);
	}

	[Fact]
	public void SanitizeEntityNameReplacesInvalidCharactersWithDashes()
	{
		// ':' AND ' ' ARE NOT VALID SERVICE BUS ENTITY NAME CHARACTERS
		var result = AzureServiceBusHelpers.SanitizeEntityName("MyCache.Backplane:v1", 260);

		Assert.Equal("MyCache.Backplane-v1", result);
		Assert.DoesNotContain(':', result);
	}

	[Fact]
	public void SanitizeEntityNameTrimsLeadingAndTrailingSeparators()
	{
		var result = AzureServiceBusHelpers.SanitizeEntityName("///cache-name---", 260);

		Assert.Equal("cache-name", result);
	}

	[Fact]
	public void SanitizeEntityNameTruncatesToMaxLength()
	{
		var longName = new string('a', 300);

		var result = AzureServiceBusHelpers.SanitizeEntityName(longName, 50);

		Assert.Equal(50, result.Length);
	}

	[Fact]
	public void SanitizeEntityNameReturnsFallbackWhenResultWouldBeEmpty()
	{
		var result = AzureServiceBusHelpers.SanitizeEntityName("::: ***", 50, fallback: "my-fallback");

		Assert.Equal("my-fallback", result);
	}

	[Fact]
	public void ResolveTopicNameUsesExplicitTopicNameWhenProvided()
	{
		var result = AzureServiceBusHelpers.ResolveTopicName("my-explicit-topic", "MyCache.Backplane:v1");

		Assert.Equal("my-explicit-topic", result);
	}

	[Fact]
	public void ResolveTopicNameFallsBackToChannelNameWhenNotProvided()
	{
		var result = AzureServiceBusHelpers.ResolveTopicName(null, "MyCache.Backplane:v1");

		Assert.Equal("MyCache.Backplane-v1", result);
	}

	[Fact]
	public void ResolveTopicNameFallsBackToChannelNameWhenExplicitTopicNameIsWhitespace()
	{
		var result = AzureServiceBusHelpers.ResolveTopicName("   ", "MyCache.Backplane:v1");

		Assert.Equal("MyCache.Backplane-v1", result);
	}
	[Fact]
	public void GenerateIdReturnsAValidSubscriptionNameLength()
	{
		var id = AzureServiceBusHelpers.GenerateId();

		Assert.True(id.Length <= AzureServiceBusHelpers.MaxSubscriptionNameLength, $"Expected length <= {AzureServiceBusHelpers.MaxSubscriptionNameLength}, but was {id.Length} ('{id}')");
		Assert.NotEmpty(id);
	}

	[Fact]
	public void GenerateIdOnlyContainsValidServiceBusEntityNameCharacters()
	{
		var id = AzureServiceBusHelpers.GenerateId();

		foreach (var c in id)
		{
			Assert.True(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '/', $"Unexpected character '{c}' in generated id '{id}'");
		}
	}

	[Fact]
	public void GenerateIdReturnsDifferentValuesOnSuccessiveCalls()
	{
		var id1 = AzureServiceBusHelpers.GenerateId();
		var id2 = AzureServiceBusHelpers.GenerateId();

		Assert.NotEqual(id1, id2);
	}
}
