using BenchmarkDotNet.Running;
using System;
using System.Threading.Tasks;

namespace ZiggyCreatures.FusionCaching.Benchmarks
{
	class Program
	{

		static void RunAsyncCacheComparisonBenchmark()
		{
			_ = BenchmarkRunner.Run<AsyncCacheComparisonBenchmark>();

			// EXCESSIVE FACTORY CALLS
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("EXCESSIVE FACTORY CALLS");
			Console.WriteLine();

			if (AsyncCacheComparisonBenchmark.ExcessiveFactoryCalls.Count == 0)
			{
				Console.WriteLine("- NONE");
			}
			else
			{
				foreach (var kvp in AsyncCacheComparisonBenchmark.ExcessiveFactoryCalls)
				{
					Console.WriteLine($"- {kvp.Key}:");
					foreach (var stat in kvp.Value)
					{
						Console.WriteLine($" - {stat}:");
					}
				}
			}
		}

		static void RunSyncCacheComparisonBenchmark()
		{
			_ = BenchmarkRunner.Run<SyncCacheComparisonBenchmark>();

			// EXCESSIVE FACTORY CALLS
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("EXCESSIVE FACTORY CALLS");
			Console.WriteLine();

			if (SyncCacheComparisonBenchmark.ExcessiveFactoryCalls.Count == 0)
			{
				Console.WriteLine("- NONE");
			}
			else
			{
				foreach (var kvp in SyncCacheComparisonBenchmark.ExcessiveFactoryCalls)
				{
					Console.WriteLine($"- {kvp.Key}:");
					foreach (var stat in kvp.Value)
					{
						Console.WriteLine($" - {stat}:");
					}
				}
			}
		}

		public static async Task Main(string[] args)
		{
			RunAsyncCacheComparisonBenchmark();
			//RunSyncCacheComparisonBenchmark();
		}

	}
}