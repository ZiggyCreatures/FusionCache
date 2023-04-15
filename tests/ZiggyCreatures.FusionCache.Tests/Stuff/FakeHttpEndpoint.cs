using System;

namespace FusionCacheTests.Stuff
{
	internal class FakeHttpEndpoint
	{
		public FakeHttpEndpoint(int value)
		{
			SetValue(value);
		}

		private int Value { get; set; }
		private DateTimeOffset? LastModified { get; set; }

		public int TotalRequestsCount { get; private set; }
		public int ConditionalRequestsCount { get; private set; }
		public int FullResponsesCount { get; private set; }
		public int NotModifiedResponsesCount { get; private set; }

		public void SetValue(int value)
		{
			Value = value;
			LastModified = DateTimeOffset.UtcNow;
		}

		public FakeHttpResponse Get(DateTimeOffset? ifModifiedSince = null)
		{
			TotalRequestsCount++;

			if (ifModifiedSince is not null)
				ConditionalRequestsCount++;

			if (ifModifiedSince is null || ifModifiedSince < LastModified)
			{
				// FULL RESPONSE
				FullResponsesCount++;
				return new FakeHttpResponse
				{
					NotModified = false,
					Value = Value,
					LastModified = LastModified
				};
			}

			// NOT MODIFIED RESPONSE
			NotModifiedResponsesCount++;
			return new FakeHttpResponse
			{
				NotModified = true,
				Value = null,
				LastModified = LastModified
			};
		}
	}
}
