using System;

namespace ZiggyCreatures.Caching.Fusion.Events
{
	public class FusionCacheCircuitBreakerChangeEventArgs : EventArgs
	{
		public FusionCacheCircuitBreakerChangeEventArgs(bool isClosed)
		{
			IsClosed = isClosed;
		}

		public bool IsClosed { get; }
	}
}
