using System;

namespace FusionCacheTests
{
	static class TestsExtMethods
	{
		public static TimeSpan PlusALittleBit(this TimeSpan ts)
		{
			return ts + TimeSpan.FromMilliseconds(100);
		}
	}
}
