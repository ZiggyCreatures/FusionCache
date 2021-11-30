using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks
{
	class Program
	{
		public static async Task Main(string[] args)
		{
			BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run();
		}
	}
}
