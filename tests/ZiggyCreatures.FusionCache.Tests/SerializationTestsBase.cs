using FusionCacheTests.Stuff;
using Xunit;

namespace FusionCacheTests;

public partial class SerializationTestsBase
	: AbstractTests
{
	private static readonly ComplexType[] BigData;

	static SerializationTestsBase()
	{
		var len = 1024 * 1024;
		BigData = new ComplexType[len];
		for (int i = 0; i < len; i++)
		{
			BigData[i] = ComplexType.CreateSample();
		}
	}

	public SerializationTestsBase(ITestOutputHelper output)
			: base(output, null)
	{
	}

	private const string SampleString = "Supercalifragilisticexpialidocious";
}
