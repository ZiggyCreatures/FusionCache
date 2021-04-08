using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace ZiggyCreatures.FusionCaching.AppMetrics.Benchmarks
{
    class Program
    {
        public static async Task Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        
    }
}
