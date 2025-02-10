using FusionCacheTests.Stuff;
using Xunit.Abstractions;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

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

	[Fact]
	public void DeserializeWorksCorrectlyAfterAppRestart_NewtonsoftJson()
	{
		var serializer = new FusionCacheNewtonsoftJsonSerializer();
		var jsonData = "{\"Name\":\"John\",\"Age\":30}";
		var data = System.Text.Encoding.UTF8.GetBytes(jsonData);
		var deserializedObject = serializer.Deserialize<ComplexType>(data);
		Assert.NotNull(deserializedObject);
		Assert.Equal("John", deserializedObject.Name);
		Assert.Equal(30, deserializedObject.Age);
	}

	[Fact]
	public void DeserializeWorksCorrectlyAfterAppRestart_SystemTextJson()
	{
		var serializer = new FusionCacheSystemTextJsonSerializer();
		var jsonData = "{\"Name\":\"John\",\"Age\":30}";
		var data = System.Text.Encoding.UTF8.GetBytes(jsonData);
		var deserializedObject = serializer.Deserialize<ComplexType>(data);
		Assert.NotNull(deserializedObject);
		Assert.Equal("John", deserializedObject.Name);
		Assert.Equal(30, deserializedObject.Age);
	}
}
