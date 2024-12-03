using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using FASTERCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Spectre.Console.Rendering;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.Memory;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Chaos;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Simulator.Stuff;

namespace ZiggyCreatures.Caching.Fusion.Playground.Simulator;

internal static class SimulatorOptions
{
	// GENERAL
	public static int ClustersCount = 1;
	public static int NodesPerClusterCount = 2;
	public static bool EnableFailSafe = false;
	public static readonly TimeSpan RandomUpdateDelay = TimeSpan.FromSeconds(1);
	public static bool EnableRandomUpdates = false;
	public static readonly bool DisplayApproximateExpirationCountdown = false;

	// DURATION
	public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

	// LOGGING
	public static readonly bool EnableFusionCacheLogging = false;
	public static readonly bool EnableSimulatorLogging = false;
	public static readonly bool EnableLoggingExceptions = false;

	private static readonly string RedisConnection = "127.0.0.1:6379,ssl=False,abortConnect=false,defaultDatabase={0},connectTimeout=1000,syncTimeout=1000";

	// DISTRIBUTED CACHE
	public static readonly DistributedCacheType DistributedCacheType = DistributedCacheType.Memory;
	public static readonly bool AllowBackgroundDistributedCacheOperations = true;
	public static readonly TimeSpan? DistributedCacheSoftTimeout = null; //TimeSpan.FromMilliseconds(100);
	public static readonly TimeSpan? DistributedCacheHardTimeout = null; //TimeSpan.FromMilliseconds(500);
	public static readonly TimeSpan DistributedCacheCircuitBreakerDuration = TimeSpan.Zero;
	public static readonly string DistributedCacheRedisConnection = RedisConnection;
	public static readonly TimeSpan? ChaosDistributedCacheSyntheticMinDelay = null; //TimeSpan.FromMilliseconds(500);
	public static readonly TimeSpan? ChaosDistributedCacheSyntheticMaxDelay = null; //TimeSpan.FromMilliseconds(500);

	// BACKPLANE
	public static readonly BackplaneType BackplaneType = BackplaneType.Memory;
	public static readonly bool AllowBackgroundBackplaneOperations = true;
	public static readonly TimeSpan BackplaneCircuitBreakerDuration = TimeSpan.Zero;
	public static readonly string BackplaneRedisConnection = RedisConnection;
	public static readonly TimeSpan? ChaosBackplaneSyntheticDelay = null; //TimeSpan.FromMilliseconds(500);

	// AUTO-RECOVERY
	public static readonly bool EnableAutoRecovery = true;
	public static readonly TimeSpan? AutoRecoveryDelay = null;
	public static readonly TimeSpan AutoRecoveryDefaultDelay = new FusionCacheOptions().AutoRecoveryDelay;

	// OTHERS
	public static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(500);
	public static readonly TimeSpan DataChangesMinDelay = TimeSpan.FromSeconds(1);
	public static readonly TimeSpan DataChangesMaxDelay = TimeSpan.FromSeconds(1);
	public static readonly bool UpdateCacheOnSaveToDb = true;
	public static readonly TimeSpan? PostUpdateCooldownDelay = TimeSpan.FromMilliseconds(150);
}

internal class Program
{
	// INTERNAL
	private static string CacheKey = "foo";
	private static readonly Random RNG = new Random();
	private static readonly SemaphoreSlim GlobalMutex = new SemaphoreSlim(1, 1);
	private static int LastValue = 0;
	private static int? LastUpdatedClusterIdx = null;
	private static readonly ConcurrentDictionary<int, SimulatedCluster> CacheClusters = [];
	private static readonly ConcurrentDictionary<int, SimulatedDatabase> Databases = [];

	private static bool DatabaseEnabled = true;

	private static readonly List<ChaosDistributedCache> DistributedCaches = [];
	private static bool DistributedCachesEnabled = true;

	private static readonly List<ChaosBackplane> Backplanes = [];
	private static bool BackplanesEnabled = true;

	// STATS
	private static int DbWritesCount = 0;
	private static int DbReadsCount = 0;

	// COLORS
	private static readonly Color Color_DarkGreen = Color.DarkGreen;
	private static readonly Color Color_MidGreen = Color.SpringGreen3;
	private static readonly Color Color_LightGreen = Color.SpringGreen2;
	private static readonly Color Color_FlashGreen = Color.SpringGreen3_1;
	private static readonly Color Color_DarkRed = Color.DarkRed;
	private static readonly Color Color_MidRed = Color.DeepPink2;
	private static readonly Color Color_LightRed = Color.Red3_1;
	private static readonly Color Color_FlashRed = Color.Red1;

	// NOTE: THIS SEEMS TO HAVE PROBLEMS WHEN CONNECTING MULTIPLE TIMES TO THE SAME REDIS INSTANCE
	// FROM THE SAME PROCESS, PARTICULARLY REGARDING PUBSUB
	private static bool ReUseConnectionMultiplexers = false;

	private static ConcurrentDictionary<string, IConnectionMultiplexer> _connectionMultiplexerCache = new ConcurrentDictionary<string, IConnectionMultiplexer>();

	private static IConnectionMultiplexer GetRedisConnectionMultiplexer(int clusterIdx, int nodeIdx)
	{
		var configuration = string.Format(SimulatorOptions.BackplaneRedisConnection, clusterIdx);

		if (ReUseConnectionMultiplexers)
			return _connectionMultiplexerCache.GetOrAdd($"C{clusterIdx}_N{nodeIdx}", x => ConnectionMultiplexer.Connect(configuration));

		return ConnectionMultiplexer.Connect(configuration);
	}

	private static IDistributedCache? CreateDistributedCache(int clusterIdx, IServiceProvider serviceProvider)
	{
		switch (SimulatorOptions.DistributedCacheType)
		{
			case DistributedCacheType.None:
				return null;
			case DistributedCacheType.Redis:
				return new RedisCache(new RedisCacheOptions
				{
					ConnectionMultiplexerFactory = async () => GetRedisConnectionMultiplexer(clusterIdx, -1)
				});
			case DistributedCacheType.FASTER:
				var directory = Path.Combine(Path.GetTempPath(), $"FusionCacheSimulator_{clusterIdx}");
				Debug.WriteLine($"DIRECTORY: {directory}");
				return new FASTERCacheBuilder(directory).CreateDistributedCache();
			default:
				return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
		}
	}

	private static IFusionCacheBackplane? CreateBackplane(int clusterIdx, int nodeIdx, IServiceProvider serviceProvider)
	{
		switch (SimulatorOptions.BackplaneType)
		{
			case BackplaneType.None:
				return null;
			case BackplaneType.Redis:
				return new RedisBackplane(
					new RedisBackplaneOptions
					{
						ConnectionMultiplexerFactory = async () => GetRedisConnectionMultiplexer(clusterIdx, nodeIdx)
					},
					SimulatorOptions.EnableFusionCacheLogging ? serviceProvider.GetService<ILogger<RedisBackplane>>() : null
				);
			default:
				return new MemoryBackplane(
					new MemoryBackplaneOptions()
					{
						ConnectionId = $"connection-{clusterIdx}"
					},
					SimulatorOptions.EnableFusionCacheLogging ? serviceProvider.GetService<ILogger<MemoryBackplane>>() : null
				);
		}
	}

	private static void SaveToDb(int clusterIdx, int value)
	{
		if (DatabaseEnabled == false)
			throw new Exception("Synthetic database exception");

		Interlocked.Increment(ref DbWritesCount);

		var db = Databases.GetOrAdd(clusterIdx, _ => new SimulatedDatabase());
		db.Value = value;
		db.LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		LastUpdatedClusterIdx = clusterIdx;
	}

	private static int? LoadFromDb(int clusterIdx)
	{
		if (DatabaseEnabled == false)
			throw new Exception("Synthetic database exception");

		Interlocked.Increment(ref DbReadsCount);

		var db = Databases.GetOrAdd(clusterIdx, _ => new SimulatedDatabase());
		return db.Value;
	}

	private static async Task UpdateRandomNodeOnClusterAsync(int clusterIdx, ILogger<FusionCache>? logger)
	{
		var sw = Stopwatch.StartNew();
		await GlobalMutex.WaitAsync();
		sw.Stop();
		logger?.LogInformation("LOCK (UPDATE) TOOK: {ElapsedMs} ms", sw.ElapsedMilliseconds);

		try
		{
			// CHANGE THE VALUE
			LastValue++;

			// SAVE TO DB
			try
			{
				SaveToDb(clusterIdx, LastValue);

				// UPDATE CACHE
				if (SimulatorOptions.UpdateCacheOnSaveToDb)
				{
					var cluster = CacheClusters[clusterIdx];
					var nodeIdx = RNG.Next(cluster.Nodes.Count);
					var node = cluster.Nodes[nodeIdx];

					logger?.LogInformation("BEFORE CACHE SET ({CacheInstanceId}) TOOK: {ElapsedMs} ms", node.Cache.InstanceId, sw.ElapsedMilliseconds);
					sw.Restart();
					await node.Cache.SetAsync(CacheKey, LastValue, opt => opt.SetSkipBackplaneNotifications(false));
					sw.Stop();
					logger?.LogInformation("AFTER CACHE SET ({CacheInstanceId}) TOOK: {ElapsedMs} ms", node.Cache.InstanceId, sw.ElapsedMilliseconds);

					// SAVE LAST XYZ
					node.ExpirationTimestampUnixMs = DateTimeOffset.UtcNow.Add(SimulatorOptions.CacheDuration).ToUnixTimeMilliseconds();
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
				outputTemplate: SimulatorOptions.EnableLoggingExceptions
				? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
				: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}"
			)
			.CreateLogger()
		;

		services.AddLogging(configure => configure.AddSerilog());
	}

	private static DateTimeOffset? ExtractCacheEntryExpiration(IFusionCache cache, string cacheKey)
	{
		var mca = cache.GetType().GetField("_mca", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(cache);

		if (mca is null)
		{
			Debug.WriteLine("MEMORY CACHE ACCESSOR IS NULL");
			return null;
		}

		var memoryCache = (IMemoryCache?)mca.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(mca);

		if (memoryCache is null)
		{
			Debug.WriteLine("MEMORY CACHE IS NULL");
			return null;
		}

		var entry = memoryCache?.Get(cacheKey);

		if (entry is null)
		{
			Debug.WriteLine("ENTRY IS NULL");
			return null;
		}

		// GET THE LOGICAL EXPIRATION
		var metadata = (FusionCacheEntryMetadata?)entry.GetType().GetProperty("Metadata")?.GetValue(entry);
		var logicalExpiration = metadata?.LogicalExpiration;

		// GET THE PHYSICAL EXPIRATION
		DateTimeOffset? physicalExpiration = null;
		try
		{
			physicalExpiration = (DateTimeOffset?)entry.GetType().GetProperty("PhysicalExpiration")?.GetValue(entry);
		}
		catch (Exception exc)
		{
			Debug.WriteLine($"ERROR: {exc.Message}");
		}

		// WE HAVE BOTH: TAKE THE LOWER ONE
		if (logicalExpiration is not null && physicalExpiration is not null)
			return logicalExpiration.Value < physicalExpiration.Value ? logicalExpiration : physicalExpiration;

		// USE THE LOGICAL
		if (logicalExpiration is not null)
			return logicalExpiration;

		// USE THE PHYSICAL
		if (physicalExpiration is not null)
			return physicalExpiration;

		return null;
	}

	private static void SetupClusters(IServiceProvider serviceProvider, ILogger<FusionCache>? logger)
	{
		AnsiConsole.MarkupLine("[deepskyblue1]SETUP[/]");

		var swAll = Stopwatch.StartNew();
		for (int clusterIdx = 0; clusterIdx < SimulatorOptions.ClustersCount; clusterIdx++)
		{
			var swCluster = Stopwatch.StartNew();

			var cluster = new SimulatedCluster();
			var cacheName = $"C{clusterIdx + 1}";

			var distributedCache = CreateDistributedCache(clusterIdx, serviceProvider);

			for (int nodeIdx = 0; nodeIdx < SimulatorOptions.NodesPerClusterCount; nodeIdx++)
			{
				var swNode = Stopwatch.StartNew();

				var cacheInstanceId = $"{cacheName}_{nodeIdx + 1}";

				AnsiConsole.MarkupLine($"CACHE: [deepskyblue1]{cacheName} ({cacheInstanceId})[/]");

				AnsiConsole.Markup(" - [default]CORE:[/] ...");

				var options = new FusionCacheOptions()
				{
					CacheName = cacheName,
					DefaultEntryOptions = new FusionCacheEntryOptions(SimulatorOptions.CacheDuration),
					EnableAutoRecovery = SimulatorOptions.EnableAutoRecovery
				};
				if (SimulatorOptions.AutoRecoveryDelay is not null)
					options.AutoRecoveryDelay = SimulatorOptions.AutoRecoveryDelay.Value;

				options.SetInstanceId(cacheInstanceId);

				var deo = options.DefaultEntryOptions;

				// FAIL-SAFE
				deo.IsFailSafeEnabled = SimulatorOptions.EnableFailSafe;
				deo.FailSafeMaxDuration = TimeSpan.FromSeconds(60);
				deo.FailSafeThrottleDuration = TimeSpan.FromSeconds(2);

				// DISTRIBUTED CACHE
				if (SimulatorOptions.DistributedCacheSoftTimeout is not null)
					deo.DistributedCacheSoftTimeout = SimulatorOptions.DistributedCacheSoftTimeout.Value;
				if (SimulatorOptions.DistributedCacheHardTimeout is not null)
					deo.DistributedCacheHardTimeout = SimulatorOptions.DistributedCacheHardTimeout.Value;
				deo.AllowBackgroundDistributedCacheOperations = SimulatorOptions.AllowBackgroundDistributedCacheOperations;
				options.DistributedCacheCircuitBreakerDuration = SimulatorOptions.DistributedCacheCircuitBreakerDuration;

				// BACKPLANE
				deo.AllowBackgroundBackplaneOperations = SimulatorOptions.AllowBackgroundBackplaneOperations;
				options.BackplaneCircuitBreakerDuration = SimulatorOptions.BackplaneCircuitBreakerDuration;

				// SPECIAL CACSE HANDLING: BACKPLANE + NO DISTRIBUTED CACHE
				if (SimulatorOptions.DistributedCacheType == DistributedCacheType.None && SimulatorOptions.BackplaneType != BackplaneType.None)
					deo.SkipBackplaneNotifications = true;

				var cacheLogger = SimulatorOptions.EnableFusionCacheLogging ? serviceProvider.GetService<ILogger<FusionCache>>() : null;
				var swCache = Stopwatch.StartNew();
				var cache = new FusionCache(options, logger: cacheLogger);
				swCache.Stop();
				logger?.LogInformation("CACHE CREATION TOOK: {ElapsedMs} ms", swCache.ElapsedMilliseconds);
				AnsiConsole.MarkupLine($" [black on {Color_DarkGreen}] OK [/]");

				// DISTRIBUTED CACHE
				if (distributedCache is not null)
				{
					AnsiConsole.Markup(" - [default]DISTRIBUTED CACHE:[/] ...");
					var chaosDistributedCacheLogger = SimulatorOptions.EnableFusionCacheLogging ? serviceProvider.GetService<ILogger<ChaosDistributedCache>>() : null;
					var tmp = new ChaosDistributedCache(distributedCache, chaosDistributedCacheLogger);
					if (SimulatorOptions.ChaosDistributedCacheSyntheticMinDelay is not null && SimulatorOptions.ChaosDistributedCacheSyntheticMaxDelay is not null)
					{
						tmp.SetAlwaysDelay(SimulatorOptions.ChaosDistributedCacheSyntheticMinDelay.Value, SimulatorOptions.ChaosDistributedCacheSyntheticMaxDelay.Value);
					}
					var swDistributedCache = Stopwatch.StartNew();
					cache.SetupDistributedCache(tmp, new FusionCacheNewtonsoftJsonSerializer());
					swDistributedCache.Stop();
					logger?.LogInformation("DISTRIBUTED CACHE SETUP TOOK: {ElapsedMs} ms", swDistributedCache.ElapsedMilliseconds);
					DistributedCaches.Add(tmp);
					AnsiConsole.MarkupLine($" [black on {Color_DarkGreen}] OK [/]");
				}

				// BACKPLANE
				var backplane = CreateBackplane(clusterIdx, nodeIdx, serviceProvider);
				if (backplane is not null)
				{
					AnsiConsole.Markup(" - [default]BACKPLANE:[/] ...");
					var chaosBackplaneLogger = SimulatorOptions.EnableFusionCacheLogging ? serviceProvider.GetService<ILogger<ChaosBackplane>>() : null;
					var tmp = new ChaosBackplane(backplane, chaosBackplaneLogger);
					if (SimulatorOptions.ChaosBackplaneSyntheticDelay is not null)
					{
						tmp.SetAlwaysDelayExactly(SimulatorOptions.ChaosBackplaneSyntheticDelay.Value);
					}
					var swBackplane = Stopwatch.StartNew();
					cache.SetupBackplane(tmp);
					swBackplane.Stop();
					logger?.LogInformation("BACKPLANE SETUP TOOK: {ElapsedMs} ms", swBackplane.ElapsedMilliseconds);
					Backplanes.Add(tmp);
					AnsiConsole.MarkupLine($" [black on {Color_DarkGreen}] OK [/]");
				}

				AnsiConsole.WriteLine();

				var node = new SimulatedNode(cache);

				// EVENTS
				cache.Events.Memory.Set += (sender, e) =>
				{
					var maybeExpiration = ExtractCacheEntryExpiration((IFusionCache)sender!, CacheKey);

					if (maybeExpiration is not null)
					{
						node.ExpirationTimestampUnixMs = maybeExpiration.Value.ToUnixTimeMilliseconds();
					}
					else
					{
						//node.ExpirationTimestamp = DateTimeOffset.UtcNow.Add(SimulatorScenarioOptions.CacheDuration).ToUnixTimeMilliseconds();
						node.ExpirationTimestampUnixMs = null;
					}
				};

				cluster.Nodes.Add(node);

				swNode.Stop();
				logger?.LogInformation("SETUP (NODE {NodeIdx}) TOOK: {ElapsedMs} ms", nodeIdx + 1, swNode.ElapsedMilliseconds);
			}

			CacheClusters[clusterIdx] = cluster;

			swCluster.Stop();
			logger?.LogInformation("SETUP (CLUSTER {ClusterIdx}) TOOK: {ElapsedMs} ms", clusterIdx + 1, swCluster.ElapsedMilliseconds);
		}

		swAll.Stop();
		logger?.LogInformation("SETUP (ALL) TOOK: {ElapsedMs} ms", swAll.ElapsedMilliseconds);
	}

	private static string GetCountdownMarkup(long nowTimestampUnixMs, long? expirationTimestampUnixMs)
	{
		if (expirationTimestampUnixMs is null || expirationTimestampUnixMs.Value <= nowTimestampUnixMs)
			return "-";

		var remainingSeconds = (expirationTimestampUnixMs.Value - nowTimestampUnixMs) / 1_000;
		var v = (float)(expirationTimestampUnixMs.Value - nowTimestampUnixMs) / (float)SimulatorOptions.CacheDuration.TotalMilliseconds;
		if (v <= 0.0f)
			return "-";

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
		static async Task GetValueFromNode(ConcurrentDictionary<int, (bool Error, int? Value)> clusterValues, int clusterIdx, SimulatedNode node, int nodeIdx, ILogger<FusionCache>? logger)
		{
			(bool Error, int? Value) item;
			try
			{
				var sw = Stopwatch.StartNew();
				//// SYNC
				//item.Value = node.Cache.GetOrSet<int?>(CacheKey, _ => LoadFromDb(clusterIdx));
				// ASYNC
				item.Value = await node.Cache.GetOrSetAsync<int?>(CacheKey, async _ => LoadFromDb(clusterIdx)).ConfigureAwait(false);
				item.Error = false;
				sw.Stop();
				logger?.LogInformation("CACHE GET ({CacheInstanceId}) TOOK: {ElapsedMs} ms", node.Cache.InstanceId, sw.ElapsedMilliseconds);
			}
			catch
			{
				item = (true, null);
				logger?.LogInformation("CACHE GET ({CacheInstanceId}) FAILED", node.Cache.InstanceId);
			}
			clusterValues[nodeIdx] = item;
		}

		var items = new List<(string Label, Table Table)>();
		var nowTimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		var swLock = Stopwatch.StartNew();
		await GlobalMutex.WaitAsync();
		swLock.Stop();
		logger?.LogInformation("LOCK (DASHBOARD) TOOK: {ElapsedMs} ms", swLock.ElapsedMilliseconds);

		try
		{
			var values = new ConcurrentDictionary<int, ConcurrentDictionary<int, (bool Error, int? Value)>>();

			if (getValues)
			{
				logger?.LogInformation("SNAPSHOT VALUES: START");

				var swClusters = Stopwatch.StartNew();

				// SNAPSHOT VALUES
				var tasks = new ConcurrentBag<Task>();

				for (int clusterIdx = 0; clusterIdx < CacheClusters.Values.Count; clusterIdx++)
				{
					var cluster = CacheClusters[clusterIdx];

					var valueCluster = values[clusterIdx] = new ConcurrentDictionary<int, (bool Error, int? Value)>();

					for (int nodeIdx = 0; nodeIdx < cluster.Nodes.Count; nodeIdx++)
					{
						var node = cluster.Nodes[nodeIdx];

						tasks.Add(GetValueFromNode(valueCluster, clusterIdx, node, nodeIdx, logger));
					}
				}

				await Task.WhenAll(tasks);

				swClusters.Stop();
				logger?.LogInformation("READ ON ALL CLUSTERS TOOK: {ElapsedMs} ms", swClusters.ElapsedMilliseconds);

				logger?.LogInformation("SNAPSHOT VALUES: END");
			}
			else
			{
				// NULL FILL VALUES
				for (int clusterIdx = 0; clusterIdx < CacheClusters.Values.Count; clusterIdx++)
				{
					var cluster = CacheClusters[clusterIdx];
					var clusterValues = values[clusterIdx] = new ConcurrentDictionary<int, (bool Error, int? Value)>();
					for (int nodeIdx = 0; nodeIdx < cluster.Nodes.Count; nodeIdx++)
					{
						clusterValues[nodeIdx] = default;
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
					var item = clusterValues[nodeIdx];

					// CELL TEXT
					var cellText = (item.Value?.ToString() ?? "-").PadRight(2).PadLeft(3);
					if (string.IsNullOrEmpty(cellText))
						cellText = " ";

					// CELL MARKUP
					string cellMarkup;
					if (item.Error)
					{
						cellMarkup = $"[white on {Color_MidRed}] X [/]";
					}
					else
					{
						var cellColor = "white";
						if (lastUpdatedNodeIdx.HasValue)
						{
							if (lastUpdatedNodeIdx.Value == nodeIdx)
							{
								if (LastUpdatedClusterIdx == clusterIdx)
									cellColor = Color_LightGreen.ToString();
								else
									cellColor = Color_DarkGreen.ToString();
							}
							else if (clusterValues[lastUpdatedNodeIdx.Value].Value == item.Value)
							{
								if (LastUpdatedClusterIdx == clusterIdx)
									cellColor = Color_LightGreen.ToString();
								else
									cellColor = Color_DarkGreen.ToString();
							}
							else
							{
								isClusterInSync = false;

								if (LastUpdatedClusterIdx == clusterIdx)
									cellColor = Color_MidRed.ToString();
								else
									cellColor = Color_DarkRed.ToString();
							}
						}
						cellMarkup = $"[{cellColor}]{cellText}[/]";
					}

					// EXTRA COUNTDOWN
					if (SimulatorOptions.DisplayApproximateExpirationCountdown)
					{
						cellMarkup += $"\n\n{GetCountdownMarkup(nowTimestampUnixMs, node.ExpirationTimestampUnixMs)}";
					}

					// BORDER COLOR
					var borderColor = Color.Black;
					if (string.IsNullOrWhiteSpace(cellText) == false && lastUpdatedNodeIdx.HasValue && lastUpdatedNodeIdx.Value == nodeIdx)
					{
						borderColor = LastUpdatedClusterIdx != clusterIdx ? Color_DarkGreen : Color_FlashGreen;
					}

					cells.Add(new Panel(new Markup(cellMarkup)).BorderColor(borderColor));
				}

				table.AddRow(cells);

				// TABLE LABEL
				var isLastUpdatedCluster = LastUpdatedClusterIdx == clusterIdx;
				var labelColor = isLastUpdatedCluster ? Color_FlashGreen.ToString() : "grey84";
				var label = $"[{labelColor}]CLUSTER C{clusterIdx + 1}[/]";

				if (isClusterInSync)
				{
					label += $" [{Color_DarkGreen} on {Color_MidGreen}] IN SYNC [/]";
				}
				else
				{
					label += $" [{Color_DarkRed} on {Color_MidRed}] NO SYNC [/]";
				}

				if (isLastUpdatedCluster)
				{
					label += $" [{Color_DarkGreen} on {Color_MidGreen}] LAST UPD [/]";
				}

				// TABLE BORDER COLOR
				var tableBorderColor = Color.Default;

				if (LastUpdatedClusterIdx is not null)
				{
					if (values[clusterIdx].Values.Any(x => x.Value is not null))
					{
						if (isClusterInSync)
						{
							if (LastUpdatedClusterIdx == clusterIdx)
								tableBorderColor = Color_MidGreen;
							else
								tableBorderColor = Color_DarkGreen;
						}
						else
						{
							if (LastUpdatedClusterIdx == clusterIdx)
								tableBorderColor = Color_MidRed;
							else
								tableBorderColor = Color_DarkRed;
						}
					}
				}

				table.BorderColor(tableBorderColor);

				// TABLE BORDER
				var tableBorder = TableBorder.Heavy;

				table.Border(tableBorder);

				items.Add((label, table));
			}

			logger?.LogInformation("DASHBOARD: END");

			// SUMMARY
			AnsiConsole.Clear();

			AnsiConsole.MarkupLine("SUMMARY");
			AnsiConsole.MarkupLine($"- [deepskyblue1]SIZE          :[/] {SimulatorOptions.NodesPerClusterCount} NODES x {SimulatorOptions.ClustersCount} CLUSTERS ({SimulatorOptions.NodesPerClusterCount * SimulatorOptions.ClustersCount} TOTAL NODES)");
			AnsiConsole.MarkupLine($"- [deepskyblue1]CACHE DURATION:[/] {SimulatorOptions.CacheDuration}");

			// AUTO-RECOVERY
			AnsiConsole.Markup("- [deepskyblue1]AUTO-RECOVERY :[/] ");
			if (SimulatorOptions.EnableAutoRecovery)
			{
				AnsiConsole.Markup($"[{Color_DarkGreen} on {Color_MidGreen}] ON [/]");
				if (SimulatorOptions.AutoRecoveryDelay.HasValue)
				{
					AnsiConsole.Markup($" - DELAY: {SimulatorOptions.AutoRecoveryDelay}");
				}
				else
				{
					AnsiConsole.Markup($" - DELAY: {SimulatorOptions.AutoRecoveryDefaultDelay} (default)");
				}
			}
			else
			{
				AnsiConsole.MarkupLine($"[{Color_DarkRed} on {Color_MidRed}] OFF [/]");
			}
			AnsiConsole.WriteLine();

			// DATABASE
			AnsiConsole.Markup("- [deepskyblue1]DATABASE      :[/] ");

			// MEMORY CACHE
			AnsiConsole.Markup($"Memory ");
			if (DatabaseEnabled)
				AnsiConsole.MarkupLine($"[{Color_DarkGreen} on {Color_MidGreen}] ON [/]");
			else
				AnsiConsole.MarkupLine($"[{Color_DarkRed} on {Color_MidRed}] OFF [/]");

			// DISTRIBUTED CACHE
			AnsiConsole.Markup("- [deepskyblue1]DIST. CACHE   :[/] ");
			if (SimulatorOptions.DistributedCacheType == DistributedCacheType.None)
			{
				AnsiConsole.MarkupLine("[red1]X NONE[/]");
			}
			else
			{
				AnsiConsole.Markup($"{SimulatorOptions.DistributedCacheType} ");
				if (DistributedCachesEnabled)
					AnsiConsole.MarkupLine($"[{Color_DarkGreen} on {Color_MidGreen}] ON [/]");
				else
					AnsiConsole.MarkupLine($"[{Color_DarkRed} on {Color_MidRed}] OFF [/]");
			}

			// BACKPLANE
			AnsiConsole.Markup("- [deepskyblue1]BACKPLANE     :[/] ");
			if (SimulatorOptions.BackplaneType == BackplaneType.None)
			{
				AnsiConsole.MarkupLine("[red1]X NONE[/]");
			}
			else
			{
				AnsiConsole.Markup($"{SimulatorOptions.BackplaneType} ");
				if (BackplanesEnabled)
					AnsiConsole.MarkupLine($"[{Color_DarkGreen} on {Color_MidGreen}] ON [/]");
				else
					AnsiConsole.MarkupLine($"[{Color_DarkRed} on {Color_MidRed}] OFF [/]");
			}
			AnsiConsole.WriteLine();

			// STATS
			AnsiConsole.MarkupLine("STATS");
			AnsiConsole.MarkupLine($"- [deepskyblue1]DATABASE      :[/] {DbWritesCount} WRITES - {DbReadsCount} READS");

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
			AnsiConsole.MarkupLine($"COMMANDS:");
			AnsiConsole.MarkupLine($"- [deepskyblue1]0[/]: enable/disable random updates (all clusters) [{(SimulatorOptions.EnableRandomUpdates ? Color_DarkGreen.ToString() : "grey78")} on {(SimulatorOptions.EnableRandomUpdates ? Color_MidGreen.ToString() : "grey19")}] {(SimulatorOptions.EnableRandomUpdates ? "ON" : "OFF")} [/]");
			AnsiConsole.MarkupLine($"- [deepskyblue1]1-{CacheClusters.Count}[/]: update a random node on the specified cluster");
			AnsiConsole.MarkupLine($"- [deepskyblue1]D/d[/]: enable/disable distributed cache (all clusters)");
			AnsiConsole.MarkupLine($"- [deepskyblue1]B/b[/]: enable/disable backplane (all clusters)");
			AnsiConsole.MarkupLine($"- [deepskyblue1]S/s[/]: enable/disable database (all clusters)");
			AnsiConsole.MarkupLine($"- [deepskyblue1]Q/q[/]: quit");
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
			inputProvided = int.TryParse(Console.ReadLine(), out SimulatorOptions.ClustersCount);
		}

		inputProvided = false;
		while (inputProvided == false)
		{
			AnsiConsole.Markup($"[deepskyblue1]NODES PER CLUSTER (amount):[/] ");
			inputProvided = int.TryParse(Console.ReadLine(), out SimulatorOptions.NodesPerClusterCount);
		}

		inputProvided = false;
		while (inputProvided == false)
		{
			AnsiConsole.Markup($"[deepskyblue1]FAIL-SAFE (y/n):[/] ");
			var tmp = Console.ReadKey();
			if (tmp.KeyChar is 'y' or 'n')
			{
				SimulatorOptions.EnableFailSafe = tmp.KeyChar == 'y';
				inputProvided = true;
			}
			else
			{
				AnsiConsole.WriteLine();
			}
		}
	}

	static async Task Main(string[] args)
	{
		Console.Title = "FusionCache - Simulator";

		CacheKey = $"foo-{DateTime.UtcNow.Ticks}";

		AnsiConsole.Clear();

		GetInputs();

		AnsiConsole.WriteLine();
		AnsiConsole.WriteLine();

		// DI
		var services = new ServiceCollection();
		SetupSerilogLogger(services, LogEventLevel.Verbose);
		var serviceProvider = services.BuildServiceProvider();

		var logger = SimulatorOptions.EnableSimulatorLogging ? serviceProvider.GetService<ILogger<FusionCache>>() : null;

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

				await Task.Delay(SimulatorOptions.RefreshDelay);
			}
		});

		_ = Task.Run(async () =>
		{
			while (ct.IsCancellationRequested == false)
			{
				if (SimulatorOptions.EnableRandomUpdates)
					await UpdateRandomNodeOnClusterAsync(RNG.Next(CacheClusters.Count), logger);
				await Task.Delay(SimulatorOptions.RandomUpdateDelay);
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
					SimulatorOptions.EnableRandomUpdates = !SimulatorOptions.EnableRandomUpdates;
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
