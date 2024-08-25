namespace FusionCacheTests.Stuff;

public enum SerializerType
{
	// JSON
	NewtonsoftJson = 0,
	SystemTextJson = 1,
	ServiceStackJson = 2,
	// MESSAGEPACK
	NeueccMessagePack = 10,
	// PROTOBUF
	ProtoBufNet = 20,
	// MEMORYPACK
	CysharpMemoryPack = 30,
}
