namespace FusionCacheTests.Stuff
{
	internal class FakeHttpEndpoint
	{
		public FakeHttpEndpoint(int initialValue)
		{
			SetValue(initialValue);
		}

		private int Value { get; set; }
		private string? ETag { get; set; }

		public int TotalRequestsCount { get; private set; }
		public int ConditionalRequestsCount { get; private set; }
		public int FullResponsesCount { get; private set; }
		public int NotModifiedResponsesCount { get; private set; }

		public void SetValue(int value)
		{
			Value = value;
			ETag = Value.GetHashCode().ToString();
		}

		public FakeHttpResponse Get(string? etag = null)
		{
			TotalRequestsCount++;

			var isRequestWithETag = string.IsNullOrWhiteSpace(etag) == false;

			if (isRequestWithETag)
				ConditionalRequestsCount++;

			if (isRequestWithETag == false || etag != ETag)
			{
				// FULL RESPONSE
				FullResponsesCount++;
				return new FakeHttpResponse(200, Value, ETag);
			}

			// NOT MODIFIED RESPONSE
			NotModifiedResponsesCount++;
			return new FakeHttpResponse(304, null, ETag);
		}
	}
}
