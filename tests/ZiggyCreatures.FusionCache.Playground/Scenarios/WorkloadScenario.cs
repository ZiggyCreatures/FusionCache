using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Spectre.Console.Rendering;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;

namespace ZiggyCreatures.Caching.Fusion.Playground.Scenarios
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
		// GENERAL
		public static int GroupsCount = 4;
		public static int NodesPerGroupCount = 10;
		public static bool EnableFailSafe = false;
		public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
		public static readonly bool EnableLogging = true;
		public static readonly bool EnableLoggingExceptions = false;

		// DISTRIBUTED CACHE
		public static readonly DistributedCacheType DistributedCacheType = DistributedCacheType.Memory;
		public static readonly bool AllowBackgroundDistributedCacheOperations = false;

		// TODO: !!! FIX THIS !!!
		//!!! I NEED TO HANDLE SOFT/HARD TIMEOUTS FOR DISTRIBUTED CACHE OPERATIONS !!! (MAYBE... CHECK WHAT SHOULD HAPPENS RELATED TO THE DB READS + DIST CACHE TIMEOUTS)

		public static readonly TimeSpan? DistributedCacheSoftTimeout = null; // TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan? DistributedCacheHardTimeout = null; // TimeSpan.FromMilliseconds(100);

		public static readonly TimeSpan DistributedCacheCircuitBreakerDuration = TimeSpan.Zero;
		public static readonly string DistributedCacheRedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False,defaultDatabase={0}";
		public static readonly TimeSpan? ChaosDistributedCacheSyntheticDelay = null; // TimeSpan.FromMilliseconds(1_500);

		// BACKPLANE
		public static readonly BackplaneType BackplaneType = BackplaneType.Memory;
		public static readonly bool AllowBackgroundBackplaneOperations = false;
		//public static readonly TimeSpan BackplaneCircuitBreakerDuration = TimeSpan.FromSeconds(10);
		public static readonly TimeSpan BackplaneCircuitBreakerDuration = TimeSpan.Zero;
		public static readonly string BackplaneRedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False,defaultDatabase={0}";
		public static readonly TimeSpan? ChaosBackplaneSyntheticDelay = null; // TimeSpan.FromMilliseconds(1_500);

		// OTHERS
		public static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);
		public static readonly TimeSpan DataChangesMinDelay = TimeSpan.FromSeconds(1);
		public static readonly TimeSpan DataChangesMaxDelay = TimeSpan.FromSeconds(1);
		public static readonly bool UpdateCacheOnSaveToDb = true;
		public static readonly TimeSpan? PostUpdateCooldownDelay = TimeSpan.FromMilliseconds(150);
	}

	public class FakeDatabase
	{
		public int? Value { get; set; }
		public long? LastUpdateTimestamp { get; set; }
	}

	public class CacheGroup
	{
		public List<CacheNode> Nodes { get; } = new List<CacheNode>();
		public int? LastUpdatedNodeIndex { get; set; }
	}

	public class CacheNode
	{
		public CacheNode(IFusionCache cache)
		{
			Cache = cache;
		}

		public IFusionCache Cache { get; }
		public long? ExpirationTimestamp { get; set; }
	}

	public static class WorkloadScenario
	{
		// INTERNAL
		private static string CacheKey = "foo";
		private static readonly Random RNG = new Random();
		private static readonly object LockObj = new object();
		private static int LastValue = 0;
		private static int? LastUpdatedGroupIdx = null;
		private static readonly ConcurrentDictionary<int, CacheGroup> CacheGroups = new ConcurrentDictionary<int, CacheGroup>();
		private static readonly ConcurrentDictionary<int, FakeDatabase> Databases = new ConcurrentDictionary<int, FakeDatabase>();

		private static bool DatabaseEnabled = true;

		private static readonly List<ChaosDistributedCache> DistributedCaches = new List<ChaosDistributedCache>();
		private static bool DistributedCachesEnabled = true;

		private static readonly List<ChaosBackplane> Backplanes = new List<ChaosBackplane>();
		private static bool BackplanesEnabled = true;

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
					return new RedisCache(new RedisCacheOptions
					{
						Configuration = string.Format(WorkloadScenarioOptions.DistributedCacheRedisConnection, groupIdx)
					});
				default:
					return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			}
		}

		private static IFusionCacheBackplane? CreateBackplane(int groupIdx)
		{
			switch (WorkloadScenarioOptions.BackplaneType)
			{
				case BackplaneType.None:
					return null;
				case BackplaneType.Redis:
					return new RedisBackplane(new RedisBackplaneOptions
					{
						Configuration = string.Format(WorkloadScenarioOptions.BackplaneRedisConnection, groupIdx),
						//CircuitBreakerDuration = WorkloadScenarioOptions.BackplaneCircuitBreakerDuration,
						//AllowBackgroundOperations = WorkloadScenarioOptions.AllowBackplaneBackgroundOperations
					});
				default:
					return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = $"connection-{groupIdx}" });
			}
		}

		private static void SaveToDb(int groupIdx, int value)
		{
			if (DatabaseEnabled == false)
			{
				throw new Exception("Synthetic database exception");
			}

			Interlocked.Increment(ref DbWritesCount);

			var db = Databases.GetOrAdd(groupIdx, new FakeDatabase());
			db.Value = value;
			db.LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			LastUpdatedGroupIdx = groupIdx;
		}

		private static int? LoadFromDb(int groupIdx)
		{
			if (DatabaseEnabled == false)
			{
				throw new Exception("Synthetic database exception");
			}

			Interlocked.Increment(ref DbReadsCount);

			var db = Databases.GetOrAdd(groupIdx, new FakeDatabase());
			return db.Value;
		}

		private static void UpdateCacheGroup(int groupIdx)
		{
			lock (LockObj)
			{
				// CHANGE THE VALUE
				LastValue++;

				// SAVE TO DB
				try
				{
					SaveToDb(groupIdx, LastValue);

					// UPDATE CACHE
					var group = CacheGroups[groupIdx];
					var nodeIdx = RNG.Next(group.Nodes.Count);
					var node = group.Nodes[nodeIdx];

					if (WorkloadScenarioOptions.UpdateCacheOnSaveToDb)
					{
						node.Cache.Set(CacheKey, LastValue);

						// SAVE LAST XYZ
						node.ExpirationTimestamp = DateTimeOffset.UtcNow.Add(WorkloadScenarioOptions.CacheDuration).ToUnixTimeMilliseconds();
						group.LastUpdatedNodeIndex = nodeIdx;
					}
				}
				catch
				{
					// EMPTY
				}
			}
		}

		private static void UpdateSomeRandomData()
		{
			// GET A RANDOM GROUP IDX
			var groupIdx = RNG.Next(CacheGroups.Count);

			UpdateCacheGroup(groupIdx);
		}

		private static void SetupSerilogLogger(IServiceCollection services, LogEventLevel minLevel = LogEventLevel.Verbose)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Is(minLevel)
				.Enrich.FromLogContext()
				.WriteTo.Debug(
					outputTemplate: WorkloadScenarioOptions.EnableLoggingExceptions
					? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
					: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}"
				)
				.CreateLogger()
			;

			services.AddLogging(configure => configure.AddSerilog());
		}

		private static void SetupCacheGroups()
		{
			AnsiConsole.MarkupLine("[deepskyblue1]SETUP[/]");
			AnsiConsole.Markup("- [deepskyblue1]SERIALIZER  : [/] CREATING...");
			AnsiConsole.MarkupLine("[green3_1]OK[/]");

			// DI
			var services = new ServiceCollection();
			SetupSerilogLogger(services, LogEventLevel.Debug);
			var serviceProvider = services.BuildServiceProvider();

			for (int groupIdx = 0; groupIdx < WorkloadScenarioOptions.GroupsCount; groupIdx++)
			{
				var group = new CacheGroup();
				var cacheName = $"C{groupIdx + 1}";

				AnsiConsole.Markup("- [deepskyblue1]DIST. CACHE : [/] CREATING...");
				var distributedCache = CreateDistributedCache(groupIdx);
				AnsiConsole.MarkupLine("[green3_1]OK[/]");

				for (int nodeIdx = 0; nodeIdx < WorkloadScenarioOptions.NodesPerGroupCount; nodeIdx++)
				{
					AnsiConsole.Markup("- [deepskyblue1]FUSION CACHE: [/] CREATING...");
					var options = new FusionCacheOptions()
					{
						CacheName = cacheName,
						InstanceId = $"{cacheName}-{nodeIdx + 1}",
						DefaultEntryOptions = new FusionCacheEntryOptions(WorkloadScenarioOptions.CacheDuration)
					};

					var deo = options.DefaultEntryOptions;

					// FAIL-SAFE
					deo.IsFailSafeEnabled = WorkloadScenarioOptions.EnableFailSafe;
					deo.FailSafeMaxDuration = TimeSpan.FromSeconds(60);
					deo.FailSafeThrottleDuration = TimeSpan.FromSeconds(2);

					// DISTRIBUTED CACHE
					if (WorkloadScenarioOptions.DistributedCacheSoftTimeout is not null)
						deo.DistributedCacheSoftTimeout = WorkloadScenarioOptions.DistributedCacheSoftTimeout.Value;
					if (WorkloadScenarioOptions.DistributedCacheHardTimeout is not null)
						deo.DistributedCacheHardTimeout = WorkloadScenarioOptions.DistributedCacheHardTimeout.Value;
					deo.AllowBackgroundDistributedCacheOperations = WorkloadScenarioOptions.AllowBackgroundDistributedCacheOperations;
					options.DistributedCacheCircuitBreakerDuration = WorkloadScenarioOptions.DistributedCacheCircuitBreakerDuration;

					// BACKPLANE
					deo.AllowBackgroundBackplaneOperations = WorkloadScenarioOptions.AllowBackgroundBackplaneOperations;
					options.BackplaneCircuitBreakerDuration = WorkloadScenarioOptions.BackplaneCircuitBreakerDuration;

					// SPECIAL CACSE HANDLING: BACKPLANE + NO DISTRIBUTED CACHE
					if (WorkloadScenarioOptions.DistributedCacheType == DistributedCacheType.None && WorkloadScenarioOptions.BackplaneType != BackplaneType.None)
						deo.SkipBackplaneNotifications = true;

					var logger = WorkloadScenarioOptions.EnableLogging ? serviceProvider.GetService<ILogger<FusionCache>>() : null;
					var cache = new FusionCache(options, logger: logger);
					AnsiConsole.MarkupLine("[green3_1]OK[/]");

					if (distributedCache is not null)
					{
						AnsiConsole.Markup("- [deepskyblue1]FUSION CACHE: [/] ADDING DIST. CACHE...");
						var tmp = new ChaosDistributedCache(distributedCache);
						if (WorkloadScenarioOptions.ChaosDistributedCacheSyntheticDelay is not null)
						{
							tmp.SetAlwaysDelayExactly(WorkloadScenarioOptions.ChaosDistributedCacheSyntheticDelay.Value);
						}
						cache.SetupDistributedCache(tmp, new FusionCacheNewtonsoftJsonSerializer());
						DistributedCaches.Add(tmp);
						AnsiConsole.MarkupLine("[green3_1]OK[/]");
					}

					AnsiConsole.Markup("- [deepskyblue1]BACKPLANE   : [/] CREATING...");
					var backplane = CreateBackplane(groupIdx);
					AnsiConsole.MarkupLine("[green3_1]OK[/]");
					if (backplane is not null)
					{
						AnsiConsole.Markup("- [deepskyblue1]FUSION CACHE: [/] ADDING BACKPLANE...");
						var tmp = new ChaosBackplane(backplane);
						if (WorkloadScenarioOptions.ChaosBackplaneSyntheticDelay is not null)
						{
							tmp.SetAlwaysDelayExactly(WorkloadScenarioOptions.ChaosBackplaneSyntheticDelay.Value);
						}
						cache.SetupBackplane(tmp);
						Backplanes.Add(tmp);
						AnsiConsole.MarkupLine("[green3_1]OK[/]");
					}

					var node = new CacheNode(cache);

					cache.Events.Memory.Set += (sender, e) =>
									{
										node.ExpirationTimestamp = DateTimeOffset.UtcNow.Add(WorkloadScenarioOptions.CacheDuration).ToUnixTimeMilliseconds();
									};

					group.Nodes.Add(node);
				}

				CacheGroups[groupIdx] = group;
			}
		}

		private static string GetCountdownMarkup(long nowTimestamp, long? expirationTimestamp)
		{
			if (expirationTimestamp is null || expirationTimestamp.Value <= nowTimestamp)
				return "";

			var remainingSeconds = (expirationTimestamp.Value - nowTimestamp) / 1000;
			var v = (float)(expirationTimestamp.Value - nowTimestamp) / (float)WorkloadScenarioOptions.CacheDuration.TotalMilliseconds;
			if (v <= 0.0f)
				return "";

			var color = "green3_1";
			switch (v)
			{
				case <= 0.1f:
					color = "red3_1";
					break;
				case <= 0.2f:
					color = "darkorange3_1";
					break;
				case <= 0.4f:
					color = "darkgoldenrod";
					break;
				case <= 0.6f:
					color = "greenyellow";
					break;
				default:
					break;
			}

			//var charIdx = (int)(v * (ProgressChars.Length - 1));
			//return $"[{color}]{ProgressChars[charIdx]}[/]";

			//return $"[{color}]-{remainingSeconds}s[/]";
			return $"[{color}]-{remainingSeconds}[/]";
		}

		//private static char GetProgressCharByTimestamp(long)
		//{

		//}

		private static void DisplayDashboard()
		{
			var tables = new List<(string Label, Table Table)>();
			var nowTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			lock (LockObj)
			{
				Debug.WriteLine("SNAPSHOT VALUES: START");

				var _values = new ConcurrentDictionary<int, ConcurrentDictionary<int, int?>>();

				var swGroups = Stopwatch.StartNew();
				// SNAPSHOT VALUES
				for (int groupIdx = 0; groupIdx < CacheGroups.Count; groupIdx++)
				{
					var _valueGroup = _values[groupIdx] = new ConcurrentDictionary<int, int?>();

					var group = CacheGroups[groupIdx];

					for (int nodeIdx = 0; nodeIdx < group.Nodes.Count; nodeIdx++)
					{
						var node = group.Nodes[nodeIdx];
						int? value;
						try
						{
							var swNode = Stopwatch.StartNew();
							value = node.Cache.GetOrSet<int?>(CacheKey, _ => LoadFromDb(groupIdx));
							swNode.Stop();
							Debug.WriteLine($"READ ON GROUP {groupIdx} NODE {nodeIdx} TOOK: {swNode.Elapsed}");
						}
						catch
						{
							value = null;
						}
						_valueGroup[nodeIdx] = value;
					}
				}
				swGroups.Stop();
				Debug.WriteLine($"TAKEN {swGroups.Elapsed}");

				Debug.WriteLine("SNAPSHOT VALUES: END");

				Debug.WriteLine("DASHBOARD: START");

				for (int groupIdx = 0; groupIdx < CacheGroups.Count; groupIdx++)
				{
					var group = CacheGroups[groupIdx];

					var table = new Table();
					table.Border = TableBorder.Heavy;

					for (int nodeIdx = 0; nodeIdx < group.Nodes.Count; nodeIdx++)
					{
						table.AddColumn(new TableColumn($"[deepskyblue1]N {nodeIdx + 1}[/]").Centered());
					}

					var lastUpdatedNodeIdx = group.LastUpdatedNodeIndex;

					var _valueGroup = _values[groupIdx];

					// BUILD CELLS
					var cells = new List<IRenderable>();
					for (int nodeIdx = 0; nodeIdx < group.Nodes.Count; nodeIdx++)
					{
						var node = group.Nodes[nodeIdx];
						var value = _valueGroup[nodeIdx];

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
							else if (_valueGroup[lastUpdatedNodeIdx.Value] == value)
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

						var text = (value?.ToString() ?? "/").PadRight(3).PadLeft(5);
						if (string.IsNullOrEmpty(text))
							text = " ";


						var borderColor = Color.Black;
						if (string.IsNullOrWhiteSpace(text) == false && lastUpdatedNodeIdx.HasValue && lastUpdatedNodeIdx.Value == nodeIdx)
						{
							borderColor = LastUpdatedGroupIdx != groupIdx ? Color.Green4 : Color.Green3_1;
						}

						cells.Add(new Panel(new Markup($"[{color}]{text}[/]\n\n{GetCountdownMarkup(nowTimestamp, node.ExpirationTimestamp)}")).BorderColor(borderColor));
					}

					table.AddRow(cells);

					var label = $"CACHE C{groupIdx + 1}";
					var labelColor = "grey84";
					if (LastUpdatedGroupIdx == groupIdx)
					{
						label += " (LAST UPDATED)";
						labelColor = "springgreen3_1";
					}

					tables.Add(($"[{labelColor}]{label}[/]", table));
				}

				Debug.WriteLine("DASHBOARD: END");

				// SUMMARY
				AnsiConsole.Clear();

				AnsiConsole.MarkupLine("SUMMARY");
				AnsiConsole.MarkupLine($"- [deepskyblue1]SIZE          :[/] GROUPS = {WorkloadScenarioOptions.GroupsCount} / NODES = {WorkloadScenarioOptions.NodesPerGroupCount}");
				AnsiConsole.MarkupLine($"- [deepskyblue1]CACHE DURATION:[/] {WorkloadScenarioOptions.CacheDuration}");
				//AnsiConsole.MarkupLine($"- [deepskyblue1]UPDATE DELAY  :[/] {WorkloadScenarioOptions.DataChangesMinDelay} - {WorkloadScenarioOptions.DataChangesMaxDelay}");

				AnsiConsole.Markup("- [deepskyblue1]DATABASE      :[/] ");
				AnsiConsole.Markup($"memory ");
				if (DatabaseEnabled)
					AnsiConsole.MarkupLine("[green3_1]v ENABLED[/]");
				else
					AnsiConsole.MarkupLine("[red1]X DISABLED[/]");

				AnsiConsole.Markup("- [deepskyblue1]DIST. CACHE   :[/] ");
				if (WorkloadScenarioOptions.DistributedCacheType == DistributedCacheType.None)
				{
					AnsiConsole.MarkupLine("[red1]X NONE[/]");
				}
				else
				{
					AnsiConsole.Markup($"{WorkloadScenarioOptions.DistributedCacheType} ");
					if (DistributedCachesEnabled)
						AnsiConsole.MarkupLine("[green3_1]v ENABLED[/]");
					else
						AnsiConsole.MarkupLine("[red1]X DISABLED[/]");
				}

				AnsiConsole.Markup("- [deepskyblue1]BACKPLANE     :[/] ");
				if (WorkloadScenarioOptions.BackplaneType == BackplaneType.None)
				{
					AnsiConsole.MarkupLine("[red1]X NONE[/]");
				}
				else
				{
					AnsiConsole.Markup($"{WorkloadScenarioOptions.BackplaneType} ");
					if (BackplanesEnabled)
						AnsiConsole.MarkupLine("[green3_1]v ENABLED[/]");
					else
						AnsiConsole.MarkupLine("[red1]X DISABLED[/]");
				}
				AnsiConsole.WriteLine();

				// STATS
				AnsiConsole.MarkupLine("STATS");
				AnsiConsole.MarkupLine($"- [deepskyblue1]DB WRITES     :[/] {DbWritesCount}");
				AnsiConsole.MarkupLine($"- [deepskyblue1]DB READS      :[/] {DbReadsCount}");

				AnsiConsole.WriteLine();

				// TABLES
				foreach (var item in tables)
				{
					// LABEL
					AnsiConsole.Markup(item.Label);

					//// DURATION COUNTDOWN
					//if (item.LastUpdatedTimestamp is not null)
					//{
					//	AnsiConsole.Markup($" [deepskyblue1]DB WRITES     :[/]   {DbWritesCount}");
					//}

					AnsiConsole.WriteLine();

					// TABLE
					AnsiConsole.Write(item.Table);
				}
				AnsiConsole.WriteLine();

				AnsiConsole.WriteLine();
				AnsiConsole.MarkupLine($"PRESS:");
				AnsiConsole.MarkupLine($" - [deepskyblue1]1-{CacheGroups.Count}[/]: set a value on a cache group");
				AnsiConsole.MarkupLine($" - [deepskyblue1]D/d[/]: enable/disable distributed cache (all groups)");
				AnsiConsole.MarkupLine($" - [deepskyblue1]B/b[/]: enable/disable backplane (all groups)");
				AnsiConsole.MarkupLine($" - [deepskyblue1]S/s[/]: enable/disable database (all groups)");
			}
		}

		public static async Task RunAsync()
		{
			CacheKey = $"foo-{DateTime.UtcNow.Ticks}";

			AnsiConsole.Clear();

			// INPUTS
			bool inputProvided;

			inputProvided = false;
			while (inputProvided == false)
			{
				AnsiConsole.Markup($"[deepskyblue1]CACHE GROUPS (amount):[/] ");
				inputProvided = int.TryParse(Console.ReadLine(), out WorkloadScenarioOptions.GroupsCount);
			}

			inputProvided = false;
			while (inputProvided == false)
			{
				AnsiConsole.Markup($"[deepskyblue1]NODES PER GROUP (amount):[/] ");
				inputProvided = int.TryParse(Console.ReadLine(), out WorkloadScenarioOptions.NodesPerGroupCount);
			}

			inputProvided = false;
			while (inputProvided == false)
			{
				AnsiConsole.Markup($"[deepskyblue1]FAIL-SAFE (y/n):[/] ");
				var tmp = Console.ReadKey();
				if (tmp.KeyChar is 'y' or 'n')
				{
					WorkloadScenarioOptions.EnableFailSafe = tmp.KeyChar == 'y';
					inputProvided = true;
				}
				else
				{
					AnsiConsole.WriteLine();
				}
			}

			SetupCacheGroups();

			using var cts = new CancellationTokenSource();
			var ct = cts.Token;

			_ = Task.Run(async () =>
			{
				while (ct.IsCancellationRequested == false)
				{
					try
					{
						// DISPLAY DASHBOARD
						DisplayDashboard();
					}
					catch (Exception exc)
					{
						AnsiConsole.Clear();
						AnsiConsole.WriteException(exc);
						throw;
					}

					await Task.Delay(WorkloadScenarioOptions.RefreshDelay).ConfigureAwait(false);
				}
			});

			var shouldExit = false;
			do
			{
				var tmp = Console.ReadKey();
				switch (tmp.KeyChar)
				{
					case '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9':
						// SET VALUE
						var groupIdx = int.Parse(tmp.KeyChar.ToString());
						if (groupIdx > 0 && groupIdx <= CacheGroups.Count)
						{
							UpdateCacheGroup(groupIdx - 1);
						}
						break;
					case 'D' or 'd':
						// TOGGLE DISTRIBUTED CACHES
						DistributedCachesEnabled = !DistributedCachesEnabled;
						foreach (var distributedCache in DistributedCaches)
						{
							if (DistributedCachesEnabled)
								distributedCache.SetNeverThrow();
							else
								distributedCache.SetAlwaysThrow();
						}
						break;
					case 'B' or 'b':
						// TOGGLE DISTRIBUTED CACHES
						BackplanesEnabled = !BackplanesEnabled;
						foreach (var backplane in Backplanes)
						{
							if (BackplanesEnabled)
								backplane.SetNeverThrow();
							else
								backplane.SetAlwaysThrow();
						}
						break;
					case 'S' or 's':
						// TOGGLE DATABASE
						DatabaseEnabled = !DatabaseEnabled;
						break;
					case 'Q' or 'q':
						// QUIT
						shouldExit = true;
						break;
					default:
						break;
				}
			} while (shouldExit == false);

			cts.Cancel();
			await Task.Delay(1_000).ConfigureAwait(false);
		}
	}
}
