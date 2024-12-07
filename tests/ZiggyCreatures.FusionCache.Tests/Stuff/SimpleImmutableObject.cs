using System.ComponentModel;
using System.Runtime.Serialization;
using MemoryPack;

namespace FusionCacheTests.Stuff;

[ImmutableObject(true)]
[DataContract]
[MemoryPackable]
internal sealed partial class SimpleImmutableObject
{
	[DataMember(Name = "name", Order = 1)]
	public string? Name { get; init; }
	[DataMember(Name = "age", Order = 2)]
	public int Age { get; init; }
}
