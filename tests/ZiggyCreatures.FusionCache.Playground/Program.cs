using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion.Playground.Scenarios;

namespace ZiggyCreatures.Caching.Fusion.Playground
{
	class Program
	{
		static async Task Main(string[] args)
		{
			//await LoggingScenario.RunAsync().ConfigureAwait(false);
			await OpenTelemetryScenario.RunAsync().ConfigureAwait(false);
		}
	}
}
