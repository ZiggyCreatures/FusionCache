using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
using ZiggyCreatures.Caching.Fusion.Internals;
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
		public static int ClustersCount = 1;
		public static int NodesPerClusterCount = 2;
		public static bool EnableFailSafe = false;
		public static readonly TimeSpan RandomUpdateDelay = TimeSpan.FromSeconds(1);
		public static bool EnableRandomUpdates = false;

		// DURATION
		public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

		// LOGGING
		public static readonly bool EnableLogging = true;
		public static readonly bool EnableLoggingExceptions = false;

		// DISTRIBUTED CACHE
		public static readonly DistributedCacheType DistributedCacheType = DistributedCacheType.Memory;
		public static readonly bool AllowBackgroundDistributedCacheOperations = true;
		public static readonly TimeSpan? DistributedCacheSoftTimeout = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan? DistributedCacheHardTimeout = TimeSpan.FromMilliseconds(100);

		public static readonly TimeSpan DistributedCacheCircuitBreakerDuration = TimeSpan.Zero;
		public static readonly string DistributedCacheRedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False,defaultDatabase={0}";
		public static readonly TimeSpan? ChaosDistributedCacheSyntheticMinDelay = null; //TimeSpan.FromMilliseconds(500);
		public static readonly TimeSpan? ChaosDistributedCacheSyntheticMaxDelay = null; //TimeSpan.FromMilliseconds(500);

		// BACKPLANE
		public static readonly BackplaneType BackplaneType = BackplaneType.Memory;
		public static readonly bool AllowBackgroundBackplaneOperations = true;
		public static readonly TimeSpan BackplaneCircuitBreakerDuration = TimeSpan.Zero;
		public static readonly string BackplaneRedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=False,defaultDatabase={0}";
		public static readonly TimeSpan? ChaosBackplaneSyntheticDelay = null; //TimeSpan.FromMilliseconds(500);

		// OTHERS
		public static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(500);
		public static readonly TimeSpan DataChangesMinDelay = TimeSpan.FromSeconds(1);
		public static readonly TimeSpan DataChangesMaxDelay = TimeSpan.FromSeconds(1);
		public static readonly bool UpdateCacheOnSaveToDb = true;
		public static readonly TimeSpan? PostUpdateCooldownDelay = TimeSpan.FromMilliseconds(150);
	}

	public class SimulatedDatabase
	{
		public int? Value { get; set; }
		public long? LastUpdateTimestamp { get; set; }
	}

	public class CacheCluster
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
		public long? ExpirationTimestampUnixMs { get; set; }
	}

	public static class WorkloadScenario
	{
		// INTERNAL
		private static string CacheKey = "foo";
		private static readonly Random RNG = new Random();
		private static readonly SemaphoreSlim GlobalMutex = new SemaphoreSlim(1, 1);
		private static int LastValue = 0;
		private static int? LastUpdatedClusterIdx = null;
		private static readonly ConcurrentDictionary<int, CacheCluster> CacheClusters = new ConcurrentDictionary<int, CacheCluster>();
		private static readonly ConcurrentDictionary<int, SimulatedDatabase> Databases = new ConcurrentDictionary<int, SimulatedDatabase>();

		private static bool DatabaseEnabled = true;

		private static readonly List<ChaosDistributedCache> DistributedCaches = new List<ChaosDistributedCache>();
		private static bool DistributedCachesEnabled = true;

		private static readonly List<ChaosBackplane> Backplanes = new List<ChaosBackplane>();
		private static bool BackplanesEnabled = true;

		// STATS
		private static int DbWritesCount = 0;
		private static int DbReadsCount = 0;

		private static IDistributedCache? CreateDistributedCache(int clusterIdx)
		{
			switch (WorkloadScenarioOptions.DistributedCacheType)
			{
				case DistributedCacheType.None:
					return null;
				case DistributedCacheType.Redis:
					return new RedisCache(new RedisCacheOptions
					{
						Configuration = string.Format(WorkloadScenarioOptions.DistributedCacheRedisConnection, clusterIdx)
					});
				default:
					return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
			}
		}

		private static IFusionCacheBackplane? CreateBackplane(int clusterIdx)
		{
			switch (WorkloadScenarioOptions.BackplaneType)
			{
				case BackplaneType.None:
					return null;
				case BackplaneType.Redis:
					return new RedisBackplane(new RedisBackplaneOptions
					{
						Configuration = string.Format(WorkloadScenarioOptions.BackplaneRedisConnection, clusterIdx),
						//CircuitBreakerDuration = WorkloadScenarioOptions.BackplaneCircuitBreakerDuration,
						//AllowBackgroundOperations = WorkloadScenarioOptions.AllowBackplaneBackgroundOperations
					});
				default:
					return new MemoryBackplane(new MemoryBackplaneOptions() { ConnectionId = $"connection-{clusterIdx}" });
			}
		}

		private static void SaveToDb(int clusterIdx, int value)
		{
			if (DatabaseEnabled == false)
			{
				throw new Exception("Synthetic database exception");
			}

			Interlocked.Increment(ref DbWritesCount);

			var db = Databases.GetOrAdd(clusterIdx, new SimulatedDatabase());
			db.Value = value;
			db.LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			LastUpdatedClusterIdx = clusterIdx;
		}

		private static int? LoadFromDb(int clusterIdx)
		{
			if (DatabaseEnabled == false)
			{
				throw new Exception("Synthetic database exception");
			}

			Interlocked.Increment(ref DbReadsCount);

			var db = Databases.GetOrAdd(clusterIdx, new SimulatedDatabase());
			return db.Value;
		}

		private static async Task UpdateRandomNodeOnClusterAsync(int clusterIdx, ILogger<FusionCache>? logger)
		{
			var sw = Stopwatch.StartNew();
			await GlobalMutex.WaitAsync();
			sw.Stop();
			logger?.LogInformation($"LOCK (UPDATE) TOOK: {sw.ElapsedMilliseconds} ms");

			try
			{
				// CHANGE THE VALUE
				LastValue++;

				// SAVE TO DB
				try
				{
					SaveToDb(clusterIdx, LastValue);

					// UPDATE CACHE
					if (WorkloadScenarioOptions.UpdateCacheOnSaveToDb)
					{
						var cluster = CacheClusters[clusterIdx];
						var nodeIdx = RNG.Next(cluster.Nodes.Count);
						var node = cluster.Nodes[nodeIdx];

						logger?.LogInformation($"BEFORE CACHE SET ({node.Cache.InstanceId}) TOOK: {sw.ElapsedMilliseconds} ms");
						sw.Restart();
						await node.Cache.SetAsync(CacheKey, LastValue, opt => opt.SetSkipBackplaneNotifications(false));
						sw.Stop();
						logger?.LogInformation($"AFTER CACHE SET ({node.Cache.InstanceId}) TOOK: {sw.ElapsedMilliseconds} ms");

						// SAVE LAST XYZ
						node.ExpirationTimestampUnixMs = DateTimeOffset.UtcNow.Add(WorkloadScenarioOptions.CacheDuration).ToUnixTimeMilliseconds();
						cluster.LastUpdatedNodeIndex = nodeIdx;
					}
				}
				catch
				{
					// EMPTY
				}
			}
			finally
			{
				GlobalMutex.Release();
			}
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

		private static DateTimeOffset? DangerouslyGetLogicalExpiration(IFusionCache cache, string cacheKey)
		{
			var dca = cache.GetType().GetField("_mca", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(cache);
			var memoryCache = (IMemoryCache?)dca?.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(dca);
			var entry = memoryCache?.Get(cacheKey);
			var meta = (FusionCacheEntryMetadata?)entry?.GetType().GetProperty("Metadata")?.GetValue(entry);
			return meta?.LogicalExpiration;
		}

		private static void SetupClusters(IServiceProvider serviceProvider, ILogger<FusionCache>? logger)
		{
			AnsiConsole.MarkupLine("[deepskyblue1]SETUP[/]");

			var swAll = Stopwatch.StartNew();
			for (int clusterIdx = 0; clusterIdx < WorkloadScenarioOptions.ClustersCount; clusterIdx++)
			{
				var swCluster = Stopwatch.StartNew();

				var cluster = new CacheCluster();
				var cacheName = $"C{clusterIdx + 1}";

				//AnsiConsole.Markup("- [deepskyblue1]DIST. CACHE : [/] CREATING...");
				var distributedCache = CreateDistributedCache(clusterIdx);
				//AnsiConsole.MarkupLine("[green3_1]OK[/]");

				for (int nodeIdx = 0; nodeIdx < WorkloadScenarioOptions.NodesPerClusterCount; nodeIdx++)
				{
					var swNode = Stopwatch.StartNew();

					var cacheInstanceId = $"{cacheName}-{nodeIdx + 1}";

					AnsiConsole.MarkupLine($"CACHE: [deepskyblue1]{cacheName} ({cacheInstanceId})[/]");

					AnsiConsole.Markup(" - [default]CORE:[/] ...");

					var options = new FusionCacheOptions()
					{
						CacheName = cacheName,
						DefaultEntryOptions = new FusionCacheEntryOptions(WorkloadScenarioOptions.CacheDuration)
					};
					options.SetInstanceId(cacheInstanceId);

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

					var cacheLogger = WorkloadScenarioOptions.EnableLogging ? serviceProvider.GetService<ILogger<FusionCache>>() : null;
					var swCache = Stopwatch.StartNew();
					var cache = new FusionCache(options, logger: cacheLogger);
					swCache.Stop();
					logger?.LogInformation($"CACHE CREATION TOOK: {swCache.ElapsedMilliseconds} ms");
					AnsiConsole.MarkupLine(" [green3_1]OK[/]");

					// DISTRIBUTED CACHE
					if (distributedCache is not null)
					{
						AnsiConsole.Markup(" - [default]DISTRIBUTED CACHE:[/] ...");
						var chaosDistributedCacheLogger = WorkloadScenarioOptions.EnableLogging ? serviceProvider.GetService<ILogger<ChaosDistributedCache>>() : null;
						var tmp = new ChaosDistributedCache(distributedCache, chaosDistributedCacheLogger);
						if (WorkloadScenarioOptions.ChaosDistributedCacheSyntheticMinDelay is not null && WorkloadScenarioOptions.ChaosDistributedCacheSyntheticMaxDelay is not null)
						{
							tmp.SetAlwaysDelay(WorkloadScenarioOptions.ChaosDistributedCacheSyntheticMinDelay.Value, WorkloadScenarioOptions.ChaosDistributedCacheSyntheticMaxDelay.Value);
						}
						var swDistributedCache = Stopwatch.StartNew();
						cache.SetupDistributedCache(tmp, new FusionCacheNewtonsoftJsonSerializer());
						swDistributedCache.Stop();
						logger?.LogInformation($"DISTRIBUTED CACHE SETUP TOOK: {swDistributedCache.ElapsedMilliseconds} ms");
						DistributedCaches.Add(tmp);
						AnsiConsole.MarkupLine(" [green3_1]OK[/]");
					}

					// BACKPLANE
					var backplane = CreateBackplane(clusterIdx);
					if (backplane is not null)
					{
						AnsiConsole.Markup(" - [default]BACKPLANE:[/] ...");
						var chaosBackplaneLogger = WorkloadScenarioOptions.EnableLogging ? serviceProvider.GetService<ILogger<ChaosBackplane>>() : null;
						var tmp = new ChaosBackplane(backplane, chaosBackplaneLogger);
						if (WorkloadScenarioOptions.ChaosBackplaneSyntheticDelay is not null)
						{
							tmp.SetAlwaysDelayExactly(WorkloadScenarioOptions.ChaosBackplaneSyntheticDelay.Value);
						}
						var swBackplane = Stopwatch.StartNew();
						cache.SetupBackplane(tmp);
						swBackplane.Stop();
						logger?.LogInformation($"BACKPLANE SETUP TOOK: {swBackplane.ElapsedMilliseconds} ms");
						Backplanes.Add(tmp);
						AnsiConsole.MarkupLine(" [green3_1]OK[/]");
					}

					AnsiConsole.WriteLine();

					var node = new CacheNode(cache);

					// EVENTS
					cache.Events.Memory.Set += (sender, e) =>
					{
						var maybeExpiration = DangerouslyGetLogicalExpiration((IFusionCache)sender!, CacheKey);

						if (maybeExpiration is not null)
						{
							node.ExpirationTimestampUnixMs = maybeExpiration.Value.ToUnixTimeMilliseconds();
						}
						else
						{
							//node.ExpirationTimestamp = DateTimeOffset.UtcNow.Add(WorkloadScenarioOptions.CacheDuration).ToUnixTimeMilliseconds();
							node.ExpirationTimestampUnixMs = null;
						}
					};

					cluster.Nodes.Add(node);

					swNode.Stop();
					logger?.LogInformation($"SETUP (NODE {nodeIdx + 1}) TOOK: {swNode.ElapsedMilliseconds} ms");
				}

				CacheClusters[clusterIdx] = cluster;

				swCluster.Stop();
				logger?.LogInformation($"SETUP (CLUSTER {clusterIdx + 1}) TOOK: {swCluster.ElapsedMilliseconds} ms");
			}

			swAll.Stop();
			logger?.LogInformation($"SETUP (ALL) TOOK: {swAll.ElapsedMilliseconds} ms");
		}

		private static string GetCountdownMarkup(long nowTimestampUnixMs, long? expirationTimestampUnixMs)
		{
			if (expirationTimestampUnixMs is null || expirationTimestampUnixMs.Value <= nowTimestampUnixMs)
				return "";

			var remainingSeconds = (expirationTimestampUnixMs.Value - nowTimestampUnixMs) / 1_000;
			var v = (float)(expirationTimestampUnixMs.Value - nowTimestampUnixMs) / (float)WorkloadScenarioOptions.CacheDuration.TotalMilliseconds;
			if (v <= 0.0f)
				return "";

			var color = "grey93";
			switch (v)
			{
				case <= 0.1f:
					color = "darkorange3_1";
					break;
				case <= 0.2f:
					color = "grey23";
					break;
				case <= 0.3f:
					color = "grey42";
					break;
				case <= 0.4f:
					color = "grey58";
					break;
				case <= 0.6f:
					color = "grey66";
					break;
				case <= 0.8f:
					color = "grey78";
					break;
				default:
					break;
			}

			return $"[{color}]-{remainingSeconds}[/]";
		}

		private static async Task DisplayDashboardAsync(ILogger<FusionCache>? logger, bool getValues)
		{
			static async Task GetValueFromNode(ConcurrentDictionary<int, int?> clusterValues, int clusterIdx, CacheNode node, int nodeIdx, ILogger<FusionCache>? logger)
			{
				int? value;
				try
				{
					var sw = Stopwatch.StartNew();
					value = node.Cache.GetOrSet<int?>(CacheKey, _ => LoadFromDb(clusterIdx));
					sw.Stop();
					logger?.LogInformation($"CACHE GET ({node.Cache.InstanceId}) TOOK: {sw.ElapsedMilliseconds} ms");
				}
				catch
				{
					value = null;
					logger?.LogInformation($"CACHE GET ({node.Cache.InstanceId}) FAILED");
				}
				clusterValues[nodeIdx] = value;
			}

			var items = new List<(string Label, Table Table)>();
			var nowTimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

			var swLock = Stopwatch.StartNew();
			await GlobalMutex.WaitAsync();
			swLock.Stop();
			logger?.LogInformation($"LOCK (DASHBOARD) TOOK: {swLock.ElapsedMilliseconds} ms");

			try
			{
				var values = new ConcurrentDictionary<int, ConcurrentDictionary<int, int?>>();

				if (getValues)
				{
					logger?.LogInformation("SNAPSHOT VALUES: START");

					var swClusters = Stopwatch.StartNew();

					// SNAPSHOT VALUES
					var tasks = new ConcurrentBag<Task>();

					for (int clusterIdx = 0; clusterIdx < CacheClusters.Values.Count; clusterIdx++)
					{
						var cluster = CacheClusters[clusterIdx];

						var valueCluster = values[clusterIdx] = new ConcurrentDictionary<int, int?>();

						for (int nodeIdx = 0; nodeIdx < cluster.Nodes.Count; nodeIdx++)
						{
							var node = cluster.Nodes[nodeIdx];

							tasks.Add(GetValueFromNode(valueCluster, clusterIdx, node, nodeIdx, logger));
						}
					}

					await Task.WhenAll(tasks);

					swClusters.Stop();
					logger?.LogInformation($"READ ON ALL CLUSTERS TOOK: {swClusters.ElapsedMilliseconds} ms");

					logger?.LogInformation("SNAPSHOT VALUES: END");
				}
				else
				{
					// NULL FILL VALUES
					for (int clusterIdx = 0; clusterIdx < CacheClusters.Values.Count; clusterIdx++)
					{
						var cluster = CacheClusters[clusterIdx];
						var clusterValues = values[clusterIdx] = new ConcurrentDictionary<int, int?>();
						for (int nodeIdx = 0; nodeIdx < cluster.Nodes.Count; nodeIdx++)
						{
							clusterValues[nodeIdx] = null;
						}
					}
				}

				logger?.LogInformation("DASHBOARD: START");

				for (int clusterIdx = 0; clusterIdx < CacheClusters.Count; clusterIdx++)
				{
					var cluster = CacheClusters[clusterIdx];

					var table = new Table();

					for (int nodeIdx = 0; nodeIdx < cluster.Nodes.Count; nodeIdx++)
					{
						table.AddColumn(new TableColumn($"[deepskyblue1]N {nodeIdx + 1}[/]").Centered());
					}

					var lastUpdatedNodeIdx = cluster.LastUpdatedNodeIndex;

					var clusterValues = values[clusterIdx];

					// BUILD CELLS
					var cells = new List<IRenderable>();
					var isClusterInSync = true;
					for (int nodeIdx = 0; nodeIdx < cluster.Nodes.Count; nodeIdx++)
					{
						var node = cluster.Nodes[nodeIdx];
						var value = clusterValues[nodeIdx];

						var color = "white";
						if (lastUpdatedNodeIdx.HasValue)
						{
							if (lastUpdatedNodeIdx.Value == nodeIdx)
							{
								if (LastUpdatedClusterIdx == clusterIdx)
									color = "green3_1";
								else
									color = "green4";
							}
							else if (clusterValues[lastUpdatedNodeIdx.Value] == value)
							{
								if (LastUpdatedClusterIdx == clusterIdx)
									color = "green3_1";
								else
									color = "green4";
							}
							else
							{
								isClusterInSync = false;

								if (LastUpdatedClusterIdx == clusterIdx)
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
							borderColor = LastUpdatedClusterIdx != clusterIdx ? Color.Green4 : Color.Green3_1;
						}

						cells.Add(new Panel(new Markup($"[{color}]{text}[/]\n\n{GetCountdownMarkup(nowTimestampUnixMs, node.ExpirationTimestampUnixMs)}")).BorderColor(borderColor));
					}

					table.AddRow(cells);

					// TABLE LABEL
					var label = $"CLUSTER C{clusterIdx + 1}";

					// TABLE LABEL COLOR
					var labelColor = "grey84";

					if (LastUpdatedClusterIdx == clusterIdx)
					{
						label += " (LAST UPDATED)";
						labelColor = "springgreen3_1";
					}

					// TABLE BORDER COLOR
					var tableBorderColor = Color.Default;

					if (LastUpdatedClusterIdx is not null)
					{
						if (values[clusterIdx].Values.Any(x => x is not null))
						{
							if (isClusterInSync)
							{
								if (LastUpdatedClusterIdx == clusterIdx)
									tableBorderColor = Color.Green3_1;
								else
									tableBorderColor = Color.Green4;
							}
							else
							{
								if (LastUpdatedClusterIdx == clusterIdx)
									tableBorderColor = Color.Red1;
								else
									tableBorderColor = Color.Red3_1;
							}
						}
					}

					table.BorderColor(tableBorderColor);

					// TABLE BORDER
					var tableBorder = TableBorder.Heavy;

					table.Border(tableBorder);

					items.Add(($"[{labelColor}]{label}[/]", table));
				}

				logger?.LogInformation("DASHBOARD: END");

				// SUMMARY
				AnsiConsole.Clear();

				AnsiConsole.MarkupLine("SUMMARY");
				AnsiConsole.MarkupLine($"- [deepskyblue1]SIZE          :[/] {WorkloadScenarioOptions.NodesPerClusterCount} NODES * {WorkloadScenarioOptions.ClustersCount} CLUSTERS ({WorkloadScenarioOptions.NodesPerClusterCount * WorkloadScenarioOptions.ClustersCount} TOTAL NODES)");
				AnsiConsole.MarkupLine($"- [deepskyblue1]CACHE DURATION:[/] {WorkloadScenarioOptions.CacheDuration}");

				AnsiConsole.Markup("- [deepskyblue1]DATABASE      :[/] ");
				AnsiConsole.Markup($"Memory ");
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
				foreach (var item in items)
				{
					// LABEL
					AnsiConsole.Markup(item.Label);

					AnsiConsole.WriteLine();

					// TABLE
					AnsiConsole.Write(item.Table);
				}
				AnsiConsole.WriteLine();

				AnsiConsole.WriteLine();
				AnsiConsole.MarkupLine($"PRESS:");
				AnsiConsole.MarkupLine($" - [deepskyblue1]0[/]: enable/disable random updates (all clusters) [grey78 on {(WorkloadScenarioOptions.EnableRandomUpdates ? "darkgreen" : "grey19")}] {(WorkloadScenarioOptions.EnableRandomUpdates ? "ON" : "OFF")} [/]");
				AnsiConsole.MarkupLine($" - [deepskyblue1]1-{CacheClusters.Count}[/]: update a random node on the specified cluster");
				AnsiConsole.MarkupLine($" - [deepskyblue1]D/d[/]: enable/disable distributed cache (all clusters)");
				AnsiConsole.MarkupLine($" - [deepskyblue1]B/b[/]: enable/disable backplane (all clusters)");
				AnsiConsole.MarkupLine($" - [deepskyblue1]S/s[/]: enable/disable database (all clusters)");
				AnsiConsole.MarkupLine($" - [deepskyblue1]Q/q[/]: quit");
			}
			finally
			{
				GlobalMutex.Release();
			}
		}

		private static void GetInputs()
		{
			// INPUTS
			bool inputProvided;

			inputProvided = false;
			while (inputProvided == false)
			{
				AnsiConsole.Markup($"[deepskyblue1]CLUSTERS (amount):[/] ");
				inputProvided = int.TryParse(Console.ReadLine(), out WorkloadScenarioOptions.ClustersCount);
			}

			inputProvided = false;
			while (inputProvided == false)
			{
				AnsiConsole.Markup($"[deepskyblue1]NODES PER CLUSTER (amount):[/] ");
				inputProvided = int.TryParse(Console.ReadLine(), out WorkloadScenarioOptions.NodesPerClusterCount);
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
		}

		public static async Task RunAsync()
		{
			CacheKey = $"foo-{DateTime.UtcNow.Ticks}";

			AnsiConsole.Clear();

			GetInputs();

			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine();

			// DI
			var services = new ServiceCollection();
			SetupSerilogLogger(services, LogEventLevel.Verbose);
			var serviceProvider = services.BuildServiceProvider();

			var logger = WorkloadScenarioOptions.EnableLogging ? serviceProvider.GetService<ILogger<FusionCache>>() : null;

			SetupClusters(serviceProvider, logger);

			using var cts = new CancellationTokenSource();
			var ct = cts.Token;

			_ = Task.Run(async () =>
			{
				var firstRun = true;
				while (ct.IsCancellationRequested == false)
				{
					try
					{
						// DISPLAY DASHBOARD
						await DisplayDashboardAsync(logger, firstRun == false);
						firstRun = false;
					}
					catch (Exception exc)
					{
						AnsiConsole.Clear();
						AnsiConsole.WriteException(exc);
						throw;
					}

					await Task.Delay(WorkloadScenarioOptions.RefreshDelay);
				}
			});

			_ = Task.Run(async () =>
			{
				while (ct.IsCancellationRequested == false)
				{
					if (WorkloadScenarioOptions.EnableRandomUpdates)
						await UpdateRandomNodeOnClusterAsync(RNG.Next(CacheClusters.Count), logger);
					await Task.Delay(WorkloadScenarioOptions.RandomUpdateDelay);
				}
			});

			var shouldExit = false;
			do
			{
				var tmp = Console.ReadKey();
				switch (tmp.KeyChar)
				{
					case '0':
						// TOGGLE RANDOM UPDATES
						WorkloadScenarioOptions.EnableRandomUpdates = !WorkloadScenarioOptions.EnableRandomUpdates;
						break;
					case '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9':
						// SET VALUE
						var clusterIdx = int.Parse(tmp.KeyChar.ToString());
						if (clusterIdx > 0 && clusterIdx <= CacheClusters.Count)
						{
							await UpdateRandomNodeOnClusterAsync(clusterIdx - 1, logger);
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
			await Task.Delay(1_000);
		}
	}
}
