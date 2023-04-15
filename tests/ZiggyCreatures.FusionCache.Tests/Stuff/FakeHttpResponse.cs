using System;

namespace FusionCacheTests.Stuff
{
	internal class FakeHttpResponse
	{
		public bool NotModified { get; set; }
		public int? Value { get; set; }
		public DateTimeOffset? LastModified { get; set; }
	}
}
