using System;
using System.Runtime.Serialization;
using MemoryPack;

namespace FusionCacheTests.Stuff
{
	[DataContract]
	[MemoryPackable]
	internal partial class FakeHttpResponse
	{
		[DataMember(Name = "nm", Order = 1)]
		public bool NotModified { get; set; }
		[DataMember(Name = "v", Order = 2)]
		public int? Value { get; set; }
		[DataMember(Name = "lm", Order = 3)]
		public DateTimeOffset? LastModified { get; set; }
	}
}
