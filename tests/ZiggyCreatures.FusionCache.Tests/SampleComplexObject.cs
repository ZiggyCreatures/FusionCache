using System;

namespace ZiggyCreatures.Caching.Fusion.Tests
{
	public class SampleComplexObject
	{

		public class SampleComplexObjectAddress
		{
			public string Country { get; set; }
			public string City { get; set; }
			public string Street { get; set; }
		}

		public int Id { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public SampleComplexObjectAddress Address { get; set; }

		public static SampleComplexObject CreateRandom()
		{
			return new SampleComplexObject
			{
				Id = DateTimeOffset.UtcNow.Second,
				FirstName = "John",
				LastName = $"Doe_{DateTimeOffset.UtcNow.Millisecond}",
				Address = new SampleComplexObjectAddress
				{
					Country = $"Country_{DateTimeOffset.UtcNow.Millisecond}",
					City = $"City_{DateTimeOffset.UtcNow.Millisecond}",
					Street = $"Street_Whatever_{DateTimeOffset.UtcNow.Millisecond}"
				}
			};
		}
	}
}