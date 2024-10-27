using System.Text.RegularExpressions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Internals;
using ZiggyCreatures.Caching.Fusion.Internals.Distributed;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.CysharpMemoryPack;
using ZiggyCreatures.Caching.Fusion.Serialization.NeueccMessagePack;
using ZiggyCreatures.Caching.Fusion.Serialization.NewtonsoftJson;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;
using ZiggyCreatures.Caching.Fusion.Serialization.ServiceStackJson;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

var serializers = new IFusionCacheSerializer[] {
	new FusionCacheProtoBufNetSerializer(),
	new FusionCacheCysharpMemoryPackSerializer(),
	new FusionCacheNeueccMessagePackSerializer(),
	new FusionCacheNewtonsoftJsonSerializer(),
	new FusionCacheSystemTextJsonSerializer(),
	new FusionCacheServiceStackJsonSerializer()
};

var generateSnapshots = false;

if (generateSnapshots)
{
	GenerateSnapshots(serializers, CreateEntry());
}
else
{
	TestSnapshots<FusionCacheDistributedEntry<string>>(serializers);
}

static void TestSnapshots<T>(IFusionCacheSerializer[] serializers)
{
	foreach (var serializer in serializers)
	{
		var assembly = typeof(FusionCache).Assembly;
		var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
		string? version = fvi.FileVersion;

		var filePrefix = $"{serializer.GetType().Name}__";

		var files = Directory.GetFiles("Snapshots", filePrefix + "*.bin");

		Console.WriteLine($"SERIALIZER: {serializer.GetType().Name} v{version ?? "?"}");
		Console.WriteLine("SNAPSHOTS:");
		foreach (var file in files)
		{
			var payloadVersion = Regex.Match(file, @"\w+__v(\d+_\d+_\d+)_\d+\.bin").Groups[1].Value.Replace('_', '.');

			var payload = File.ReadAllBytes(file);
			Console.Write($"- FROM v{payloadVersion}: ");
			try
			{
				var deserialized = serializer.Deserialize<T>(payload);
				Console.WriteLine(deserialized is null ? "FAIL" : "OK");
			}
			catch (Exception exc)
			{
				Console.WriteLine($"FAIL {exc.Message}");
			}
		}
		Console.WriteLine();
	}
}

static FusionCacheDistributedEntry<string> CreateEntry()
{
	var logicalExpiration = new DateTimeOffset(641400439934520833, TimeSpan.Zero);

	return new FusionCacheDistributedEntry<string>(
		"Sloths are cool!",
		new FusionCacheEntryMetadata(
			logicalExpiration
			, true
			, logicalExpiration.AddDays(-10)
			, "MyETagValue"
			, logicalExpiration.AddDays(-100)
		),
		DateTimeOffset.UtcNow.Ticks
	);
}

static void GenerateSnapshots<T>(IFusionCacheSerializer[] serializers, T value)
{
	var assembly = typeof(FusionCache).Assembly;
	var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
	string? version = fvi.FileVersion;

	if (string.IsNullOrWhiteSpace(version))
	{
		Console.WriteLine("Cannot establish the serializer version");
		return;
	}

	foreach (var serializer in serializers)
	{
		var filePrefix = $"{serializer.GetType().Name}__";

		var filename = $"{filePrefix}v{version!.Replace('.', '_')}.bin".ToLowerInvariant();

		var payload = serializer.Serialize(value);

		File.WriteAllBytes(filename, payload);

		Console.WriteLine("FusionCache");
		Console.WriteLine($"- VERSION : v{version ?? "?"}");
		Console.WriteLine($"- FILENAME: {filename}");
		Console.WriteLine();
	}
}
