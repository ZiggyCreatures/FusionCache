using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FusionCacheTests.Stuff;

internal class SyncOnlySerializer
	: IFusionCacheSerializer
{
	private IFusionCacheSerializer _serializer;

	public SyncOnlySerializer()
	{
		_serializer = new FusionCacheSystemTextJsonSerializer();
	}

	public byte[] Serialize<T>(T? obj)
	{
		return _serializer.Serialize(obj);
	}

	public T? Deserialize<T>(byte[] data)
	{
		return _serializer.Deserialize<T>(data);
	}

	public async ValueTask<byte[]> SerializeAsync<T>(T? obj, CancellationToken token)
	{
		throw new NotImplementedException();
	}

	public async ValueTask<T?> DeserializeAsync<T>(byte[] data, CancellationToken token)
	{
		throw new NotImplementedException();
	}
}
