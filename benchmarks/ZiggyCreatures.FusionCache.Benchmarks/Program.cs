using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	class Program
	{

		static void ExcessiveFactoryCallsSummary(Dictionary<string, List<string>> data)
		{
			// EXCESSIVE FACTORY CALLS
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("#######################");
			Console.WriteLine("EXCESSIVE FACTORY CALLS");
			Console.WriteLine("#######################");
			Console.WriteLine();

			if (data.Count == 0)
			{
				Console.WriteLine("- NONE");
			}
			else
			{
				foreach (var kvp in data)
				{
					Console.WriteLine($"- {kvp.Key}:");
					foreach (var stat in kvp.Value)
					{
						Console.WriteLine($" - {stat}:");
					}
				}
			}
		}

		static void RunAsyncCacheComparisonBenchmark()
		{
			_ = BenchmarkRunner.Run<AsyncCacheComparisonBenchmark>();
			ExcessiveFactoryCallsSummary(AsyncCacheComparisonBenchmark.ExcessiveFactoryCalls);
		}

		static void RunSyncCacheComparisonBenchmark()
		{
			_ = BenchmarkRunner.Run<SyncCacheComparisonBenchmark>();
			ExcessiveFactoryCallsSummary(SyncCacheComparisonBenchmark.ExcessiveFactoryCalls);
		}

		public static async Task Main(string[] args)
		{
			RunAsyncCacheComparisonBenchmark();
			//RunSyncCacheComparisonBenchmark();
		}

	}
}