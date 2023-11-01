using System;
using System.Threading;
using System.Threading.Tasks;
using ZiggyCreatures.Caching.Fusion;

namespace FusionCacheTests.Overloads;

// THIS THING IS JUST A WAY TO TEST THAT EVERY NEEDED PERMUTATION OF CALLS+ARGS IS AVAILABLE, BOTH SYNC AND ASYNC
internal static partial class OverloadsCallsTryouts
{
	static readonly string Key = "foo";

	static readonly Func<CancellationToken, Task<int?>> AsyncFactory = async _ => 42;
	static readonly Func<CancellationToken, int?> SyncFactory = _ => 42;

	static readonly int? DefaultValue = 42;

	static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);
	static readonly Action<FusionCacheEntryOptions> OptionsLambda = options => options.SetDuration(Duration);
	static readonly FusionCacheEntryOptions Options = new FusionCacheEntryOptions(Duration);
}
