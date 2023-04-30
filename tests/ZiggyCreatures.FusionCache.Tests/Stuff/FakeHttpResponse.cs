namespace FusionCacheTests.Stuff
{
	internal class FakeHttpResponse
	{
		public FakeHttpResponse(int statusCode, int? content, string? etag = null)
		{
			StatusCode = statusCode;
			Content = content;
			ETag = etag;
		}

		public int StatusCode { get; set; }
		public int? Content { get; set; }
		public string? ETag { get; set; }
	}
}
