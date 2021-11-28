using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Rendering;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Plugins.MemoryBackplane;
using ZiggyCreatures.Caching.Fusion.Plugins.StackExchangeRedisBackplane;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.VisualTester.Scenarios
{
	public enum DistributedCacheType
	{
		None = 0,
		Memory = 1,
		Redis = 2
	}

	public enum BackplaneType
	{
		None = 0,
		Memory = 1,
		Redis = 2
	}

	public static class WorkloadScenarioOptions
	{
		// SIZE
		public static readonly int GroupsCount = 4;
		public static readonly int NodesPerGroupCount = 5;

		// DISTRIBUTED CACHE
		public static readonly DistributedCacheType DistributedCacheType = DistributedCacheType.None;

		public static readonly bool AllowDistributedCacheBackgroundOperations = true;
		public static readonly TimeSpan? DistributedCacheSoftTimeout = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan? DistributedCacheHardTimeout = TimeSpan.FromMilliseconds(100);
		public static readonly string DistributedCacheRedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False,defaultDatabase={0}";

		// BACKPLANE
		public static readonly BackplaneType BackplaneType = BackplaneType.Memory;

		public static readonly TimeSpan? BackplaneMemoryNotificationsDelay = null; //TimeSpan.FromMilliseconds(2_000);
		public static readonly string BackplaneRedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False,defaultDatabase={0}";

		// OTHERS
		public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(20);
		public static readonly TimeSpan DataChangesMinDelay = TimeSpan.FromSeconds(1);
		public static readonly TimeSpan DataChangesMaxDelay = TimeSpan.FromSeconds(1);
		public static readonly bool UpdateCacheOnSaveToDb = false;
		public static readonly TimeSpan? PostUpdateCooldownDelay = null;
	}

	public static class WorkloadScenario
	{
		// INTERNAL
		private static string CacheKey = "foo";
		private static readonly Random RNG = new Random();
		private static readonly object LockObj = new object();
		private static int LastValue = 0;
		private static int? LastUpdatedGroupIdx = null;
		private static readonly Dictionary<int, int?> LastUpdatedCaches = new Dictionary<int, int?>();
		private static readonly List<List<IFusionCache>> CacheGroups = new List<List<IFusionCache>>();
		private static readonly Dictionary<int, int?> Databases = new Dictionary<int, int?>();

		// STATS
		private static int DbWritesCount = 0;
		private static int DbReadsCount = 0;

		private static IDistributedCache? CreateDistributedCache(int groupIdx)
		{
			switch (WorkloadScenarioOptions.DistributedCacheType)
			{
				case DistributedCacheType.None:
					return null;
				case DistributedCacheType.Redis:
					return new RedisCache(new RedisCacheOptions { Configuration = string.Format(WorkloadScenarioOptions.DistributedCacheRedisConnection, groupIdx) });
				default:
					return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			}
		}

		private static IFusionCachePlugin? CreateBackplane(int groupIdx)
		{
			switch (WorkloadScenarioOptions.BackplaneType)
			{
				case BackplaneType.None:
					return null;
				case BackplaneType.Redis:
					return new RedisBackplanePlugin(new RedisBackplaneOptions { Configuration = string.Format(WorkloadScenarioOptions.BackplaneRedisConnection, groupIdx) });
				default:
					return new MemoryBackplanePlugin(new MemoryBackplaneOptions() { NotificationsDelay = WorkloadScenarioOptions.BackplaneMemoryNotificationsDelay });
			}
		}

		private static void SaveToDb(int groupIdx, int value)
		{
			Databases[groupIdx] = value;

			Interlocked.Increment(ref DbWritesCount);
		}

		private static int? LoadFromDb(int groupIdx)
		{
			Databases.TryGetValue(groupIdx, out int? res);

			Interlocked.Increment(ref DbReadsCount);

			return res;
		}

		private static void Setup()
		{
			AnsiConsole.MarkupLine("[deepskyblue1]SETUP[/]");
			AnsiConsole.Markup("- [deepskyblue1]SERIALIZER  : [/] CREATING...");
			AnsiConsole.MarkupLine("[green3_1]OK[/]");

			for (int groupIdx = 1; groupIdx <= WorkloadScenarioOptions.GroupsCount; groupIdx++)
			{
				AnsiConsole.Markup("- [deepskyblue1]DIST. CACHE : [/] CREATING...");
				var distributedCache = CreateDistributedCache(groupIdx);
				AnsiConsole.MarkupLine("[green3_1]OK[/]");

				var nodes = new List<IFusionCache>();

				for (int nodeIdx = 1; nodeIdx <= WorkloadScenarioOptions.NodesPerGroupCount; nodeIdx++)
				{
					AnsiConsole.Markup("- [deepskyblue1]FUSION CACHE: [/] CREATING...");
					var cache = new FusionCache(new FusionCacheOptions()
					{
						CacheName = $"C{groupIdx}",
						DefaultEntryOptions = new FusionCacheEntryOptions(WorkloadScenarioOptions.CacheDuration)
							.SetFailSafe(false)
							.SetDistributedCacheTimeouts(
								WorkloadScenarioOptions.DistributedCacheSoftTimeout,
								WorkloadScenarioOptions.DistributedCacheHardTimeout,
								WorkloadScenarioOptions.AllowDistributedCacheBackgroundOperations
							)
					});
					AnsiConsole.MarkupLine("[green3_1]OK[/]");

					if (distributedCache is object)
					{
						AnsiConsole.Markup("- [deepskyblue1]FUSION CACHE: [/] ADDING DIST. CACHE...");
						cache.SetupDistributedCache(distributedCache, new FusionCacheNewtonsoftJsonSerializer());
						AnsiConsole.MarkupLine("[green3_1]OK[/]");
					}

					AnsiConsole.Markup("- [deepskyblue1]BACKPLANE   : [/] CREATING...");
					var backplane = CreateBackplane(groupIdx);
					AnsiConsole.MarkupLine("[green3_1]OK[/]");
					if (backplane is object)
					{
						AnsiConsole.Markup("- [deepskyblue1]FUSION CACHE: [/] ADDING BACKPLANE...");
						cache.AddPlugin(backplane);
						AnsiConsole.MarkupLine("[green3_1]OK[/]");
					}

					nodes.Add(cache);
				}

				CacheGroups.Add(nodes);
			}
		}

		private static void DisplayDashboard()
		{
			var tables = new List<(string Label, Table Table)>();

			lock (LockObj)
			{
				for (int groupIdx = 0; groupIdx < CacheGroups.Count; groupIdx++)
				{
					var nodes = CacheGroups[groupIdx];

					var table = new Table();
					table.Border = TableBorder.Heavy;

					for (int nodeIdx = 0; nodeIdx < nodes.Count; nodeIdx++)
					{
						table.AddColumn(new TableColumn($"[deepskyblue1]N {nodeIdx + 1}[/]").Centered());
					}

					LastUpdatedCaches.TryGetValue(groupIdx, out int? lastUpdatedNodeIdx);

					// SNAPSHOT VALUES
					var values = new Dictionary<int, int?>();
					for (int nodeIdx = 0; nodeIdx < nodes.Count; nodeIdx++)
					{
						var cache = nodes[nodeIdx];
						values[nodeIdx] = cache.GetOrSet<int?>(CacheKey, _ => LoadFromDb(groupIdx));
					}

					// BUILD CELLS
					var cells = new List<IRenderable>();
					for (int nodeIdx = 0; nodeIdx < nodes.Count; nodeIdx++)
					{
						var value = values[nodeIdx];

						var color = "white";
						if (lastUpdatedNodeIdx.HasValue)
						{
							if (lastUpdatedNodeIdx.Value == nodeIdx)
							{
								if (LastUpdatedGroupIdx == groupIdx)
									color = "green3_1";
								else
									color = "green4";
							}
							else if (values[lastUpdatedNodeIdx.Value] == value)
							{
								if (LastUpdatedGroupIdx == groupIdx)
									color = "green3_1";
								else
									color = "green4";
							}
							else
							{
								if (LastUpdatedGroupIdx == groupIdx)
									color = "red1";
								else
									color = "red3_1";
							}
						}

						var text = (value?.ToString() ?? "/").PadRight(2).PadLeft(3);
						if (string.IsNullOrEmpty(text))
							text = " ";

						if (string.IsNullOrWhiteSpace(text) == false && lastUpdatedNodeIdx.HasValue && lastUpdatedNodeIdx.Value == nodeIdx)
						{
							cells.Add(new Panel(new Markup($"[{color}]{text}[/]")).BorderColor(LastUpdatedGroupIdx != groupIdx ? Color.Green4 : Color.Green3_1));
						}
						else
						{
							cells.Add(new Panel(new Markup($"[{color}]{text}[/]")).BorderColor(Color.Black));
						}
					}

					table.AddRow(cells);

					var label = $"CACHE C{groupIdx + 1}";
					var labelColor = "grey84";
					if (LastUpdatedGroupIdx.HasValue && LastUpdatedGroupIdx.Value == groupIdx)
					{
						label += " (UPDATED)";
						labelColor = "springgreen3_1";
					}

					tables.Add(($"[{labelColor}]{label}[/]", table));
				}

				// SUMMARY
				AnsiConsole.Clear();

				AnsiConsole.MarkupLine("SUMMARY");
				AnsiConsole.MarkupLine($"- [deepskyblue1]SIZE          :[/] GROUPS = {WorkloadScenarioOptions.GroupsCount} / NODES = {WorkloadScenarioOptions.NodesPerGroupCount}");
				AnsiConsole.MarkupLine($"- [deepskyblue1]CACHE DURATION:[/] {WorkloadScenarioOptions.CacheDuration}");
				AnsiConsole.MarkupLine($"- [deepskyblue1]UPDATE DELAY  :[/] {WorkloadScenarioOptions.DataChangesMinDelay} - {WorkloadScenarioOptions.DataChangesMaxDelay}");
				if (WorkloadScenarioOptions.DistributedCacheType == DistributedCacheType.None)
					AnsiConsole.MarkupLine($"- [deepskyblue1]DIST. CACHE   :[/] [red1]X[/]");
				else
					AnsiConsole.MarkupLine($"- [deepskyblue1]DIST. CACHE   :[/] [green3_1]v[/] ({WorkloadScenarioOptions.DistributedCacheType})");

				if (WorkloadScenarioOptions.BackplaneType == BackplaneType.None)
					AnsiConsole.MarkupLine($"- [deepskyblue1]BACKPLANE     :[/] [red1]X[/]");
				else
					AnsiConsole.MarkupLine($"- [deepskyblue1]BACKPLANE     :[/] [green3_1]v[/] ({WorkloadScenarioOptions.BackplaneType})");
				AnsiConsole.WriteLine();

				// STATS
				AnsiConsole.MarkupLine("STATS");
				AnsiConsole.MarkupLine($"- [deepskyblue1]DB WRITES     :[/] {DbWritesCount}");
				AnsiConsole.MarkupLine($"- [deepskyblue1]DB READS      :[/] {DbReadsCount}");

				AnsiConsole.WriteLine();

				// TABLES
				foreach (var item in tables)
				{
					AnsiConsole.MarkupLine(item.Label);
					AnsiConsole.Write(item.Table);
				}
				AnsiConsole.WriteLine();
			}
		}

		private static void UpdateSomeRandomData()
		{
			lock (LockObj)
			{
				// GET A RANDOM GROUP IDX
				var groupIdx = RNG.Next(CacheGroups.Count);

				// CHANGE THE VALUE
				LastValue++;

				// SAVE TO DB
				SaveToDb(groupIdx, LastValue);

				// UPDATE CACHE
				var nodes = CacheGroups[groupIdx];
				var nodeIdx = RNG.Next(nodes.Count);
				var cache = nodes[nodeIdx];

				if (WorkloadScenarioOptions.UpdateCacheOnSaveToDb)
				{
					cache.Set(CacheKey, LastValue);
				}
				else
				{
					cache.Remove(CacheKey);
				}

				// SAVE LAST XYZ
				LastUpdatedGroupIdx = groupIdx;
				LastUpdatedCaches[groupIdx] = nodeIdx;
			}
		}

		public static async Task RunAsync()
		{
			CacheKey = $"foo-{DateTime.UtcNow.Ticks}";

			AnsiConsole.Clear();

			Setup();

			var cts = new CancellationTokenSource();
			var ct = cts.Token;

			while (ct.IsCancellationRequested == false)
			{
				// DISPLAY DASHBOARD
				DisplayDashboard();

				// WAIT SOME RANDOM TIME
				var delay = TimeSpan.FromMilliseconds(
					WorkloadScenarioOptions.DataChangesMinDelay.TotalMilliseconds
					+ (RNG.NextDouble() * (WorkloadScenarioOptions.DataChangesMaxDelay.TotalMilliseconds - WorkloadScenarioOptions.DataChangesMinDelay.TotalMilliseconds))
				);

				await Task.Delay(delay).ConfigureAwait(false);


				// UPDATE SOME DATA
				UpdateSomeRandomData();

				// WAIT A LITTLE TO LET THE BACKBONE TO ITS JOB
				if (WorkloadScenarioOptions.PostUpdateCooldownDelay.HasValue)
					await Task.Delay(WorkloadScenarioOptions.PostUpdateCooldownDelay.Value).ConfigureAwait(false);
			}
		}
	}
}
