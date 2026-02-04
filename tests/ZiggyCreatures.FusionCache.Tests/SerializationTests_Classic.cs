using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace FusionCacheTests;

public sealed class SerializationTests_Classic(ITestOutputHelper output) : SerializationTestsBase(output)
{
	protected override byte[] Serialize<T>(IBufferFusionCacheSerializer serializer, T sourceEntry)
	{
		return serializer.Serialize<T>(sourceEntry);
	}

	protected override T? Deserialize<T>(IBufferFusionCacheSerializer serializer, byte[] serializedData)
		where T : default
	{
		return serializer.Deserialize<T>(serializedData);
	}

	protected override ValueTask<byte[]> SerializeAsync<T>(IBufferFusionCacheSerializer serializer, T? obj, CancellationToken ct) 
		where T : default
	{
		return serializer.SerializeAsync(obj, ct);
	}

	protected override ValueTask<T?> DeserializeAsync<T>(IBufferFusionCacheSerializer serializer, byte[] data, CancellationToken ct)
		where T : default
	{
		return serializer.DeserializeAsync<T>(data, ct);
	}
}
