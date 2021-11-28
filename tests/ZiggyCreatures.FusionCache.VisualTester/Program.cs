using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.VisualTester.Scenarios;

namespace ZiggyCreatures.Caching.Fusion.VisualTester
{
	class Program
	{
		static async Task Main(string[] args)
		{
			//await LoggingScenario.RunAsync().ConfigureAwait(false);
			await WorkloadScenario.RunAsync().ConfigureAwait(false);
		}
	}
}
