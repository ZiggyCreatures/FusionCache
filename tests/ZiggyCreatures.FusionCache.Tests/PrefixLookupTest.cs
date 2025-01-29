using System.Linq;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests;

public class PrefixLookupTests
{
	[Fact]
	public void TryFind_EmptyLookup_ReturnsDefault()
	{
		var prefixLookup = CreateLookup();

		var result = prefixLookup.TryFind("someKey");

		Assert.Null(result);
	}

	[Fact]
	public void TryFind_SingleMatchingValue_ReturnsValue()
	{
		var prefixLookup = CreateLookup(
			("someKey", "someValue"),
			("otherKey", "otherValue"));

		var result = prefixLookup.TryFind("someKey");

		Assert.Equal("someValue", result);
	}

	[Fact]
	public void TryFind_NoMatchingValue_ReturnsDefault()
	{
		var prefixLookup = CreateLookup(
			("otherKey1", "otherValue1"),
			("otherKey2", "otherValue2"));

		var result1 = prefixLookup.TryFind("aKey"); // before min existing key
		var result2 = prefixLookup.TryFind("someKey"); // after max existing key

		Assert.Null(result1);
		Assert.Null(result2);
	}

	[Fact]
	public void TryFind_SingleMatchingPrefix_ReturnsValue()
	{
		var prefixLookup = CreateLookup(
			("some", "someValue"),
			("otherKey", "otherValue"));

		var result = prefixLookup.TryFind("someKey-123");

		Assert.Equal("someValue", result);
	}

	[Fact]
	public void TryFind_MultipleMatchingPrefixes_ReturnsValueForLongestPrefix()
	{
		var prefixLookup = CreateLookup(
			("s", "otherValue1"),
			("some", "otherValue2"),
			("someKey", "someValue"),
			("someKey-2", "someOtherValue"));

		var result = prefixLookup.TryFind("someKey-123");

		Assert.Equal("someValue", result);
	}

	private static PrefixLookup<string> CreateLookup(params (string Prefix, string Value)[] values) =>
		new(values.ToDictionary(
			tuple => tuple.Prefix, 
			tuple => tuple.Value)!);
}
