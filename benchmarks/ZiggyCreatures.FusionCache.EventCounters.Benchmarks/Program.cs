using System.Threading.Tasks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace ZiggyCreatures.FusionCaching.EventCounters.Benchmarks
{
    class Program
    {
        public static async Task Main(string[] args)  
            => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(
                    args
                     // , new DebugInProcessConfig()
                    );
    }
}
