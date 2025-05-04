using FusionCacheTests.Stuff;
using Xunit;

namespace FusionCacheTests;

public partial class SerializationTests
	: AbstractTests
{
	private static readonly ComplexType[] BigData;

	static SerializationTests()
	{
		var len = 1024 * 1024;
		BigData = new ComplexType[len];
		for (int i = 0; i < len; i++)
		{
			BigData[i] = ComplexType.CreateSample();
		}
	}

	public SerializationTests(ITestOutputHelper output)
			: base(output, null)
	{
	}

	private const string SampleString = "Supercalifragilisticexpialidocious";
}
