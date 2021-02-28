using BenchmarkDotNet.Running;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	class Program
	{

		public static async Task Main(string[] args)
		{
			_ = BenchmarkRunner.Run<AsyncComparisonBenchmark>();
			//_ = BenchmarkRunner.Run<SyncComparisonBenchmark>();
		}

	}
}