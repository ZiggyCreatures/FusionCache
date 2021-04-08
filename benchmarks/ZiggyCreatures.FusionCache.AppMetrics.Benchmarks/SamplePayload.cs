namespace ZiggyCreatures.FusionCaching.AppMetrics.Benchmarks
{
	public class SamplePayload
	{
		public SamplePayload()
		{
			Foo = "foo";
			Bar = "bar";
			Baz = 42;
		}

		public string Foo { get; set; }
		public string Bar { get; set; }
		public int Baz { get; set; }
	}
}