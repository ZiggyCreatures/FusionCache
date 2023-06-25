using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	[MemoryDiagnoser]
	[Config(typeof(Config))]
	public class OperationIdGenerationBenchmark
	{
		private class Config : ManualConfig
		{
			public Config()
			{
				AddColumn(
					StatisticColumn.P95
				);
			}
		}

		[Benchmark(Baseline = true)]
		public string V1()
		{
			return FusionCacheInternalUtils.GenerateOperationId_V1();
		}

		[Benchmark]
		public string V2()
		{
			return FusionCacheInternalUtils.GenerateOperationId_V2();
		}

		[Benchmark]
		public string V3()
		{
			return FusionCacheInternalUtils.GenerateOperationId_V3();
		}
	}
}

/*
RESULTS

|     Method |      Mean |    Error |   StdDev |       P95 | Ratio |  Gen 0 | Allocated |
|----------- |----------:|---------:|---------:|----------:|------:|-------:|----------:|
|    Current | 196.82 ns | 0.405 ns | 0.359 ns | 197.32 ns |  1.00 | 0.0210 |      88 B |
|  Optimized |  25.36 ns | 0.070 ns | 0.062 ns |  25.46 ns |  0.13 | 0.0249 |     104 B |
| Optimized2 |  32.60 ns | 0.343 ns | 0.268 ns |  33.06 ns |  0.17 | 0.0114 |      48 B |
*/
