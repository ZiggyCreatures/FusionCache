using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	class Program
	{

		public static async Task Main(string[] args)
		{
			_ = BenchmarkRunner.Run<ParallelComparisonBenchmark>();
			//_ = BenchmarkRunner.Run<SequentialComparisonBenchmarkAsync>();
			//_ = BenchmarkRunner.Run<SequentialComparisonBenchmarkSync>();
		}

	}
}
