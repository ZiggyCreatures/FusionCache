using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Microsoft.IO;
using ZiggyCreatures.Caching.Fusion.Serialization;
using ZiggyCreatures.Caching.Fusion.Serialization.ProtoBufNet;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

public abstract class AbstractSerializersBenchmark
{
	protected static Random _MyRandom = new Random(2110);

	[DataContract]
	protected class SampleModel
	{
		[DataMember(Order = 1)]
		public string? Name { get; set; }
		[DataMember(Order = 2)]
		public int Age { get; set; }
		[DataMember(Order = 3)]
		public DateTime Date { get; set; }
		[DataMember(Order = 4)]
		public List<int> FavoriteNumbers { get; set; } = [];

		public static SampleModel GenerateRandom()
		{
			var model = new SampleModel
			{
				Name = Guid.NewGuid().ToString("N"),
				Age = _MyRandom.Next(1, 100),
				Date = DateTime.UtcNow,
			};
			for (int i = 0; i < 10; i++)
			{
				model.FavoriteNumbers.Add(_MyRandom.Next(1, 1000));
			}
			return model;
		}
	}

	protected class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
		}
	}

	protected IFusionCacheSerializer _Normal = null!;
	protected IFusionCacheSerializer _Recyclable = null!;
	protected List<SampleModel> _Models = new List<SampleModel>();
	protected byte[] _Blob = null!;

	[Params(1, 100, 1_000)]
	public int Size;

	[GlobalSetup]
	public void Setup()
	{
		for (int i = 0; i < Size; i++)
		{
			_Models.Add(SampleModel.GenerateRandom());
		}
		_Blob = _Normal.Serialize(_Models);
	}

	[Benchmark(Baseline = true)]
	public void Serialize_Normal()
	{
		_Normal.Serialize(_Models);
	}

	[Benchmark]
	public void Serialize_Recyclable()
	{
		_Recyclable.Serialize(_Models);
	}

	[Benchmark]
	public void Deserialize_Normal()
	{
		_Normal.Deserialize<List<SampleModel>>(_Blob);
	}

	[Benchmark]
	public void Deserialize_Recyclable()
	{
		_Recyclable.Deserialize<List<SampleModel>>(_Blob);
	}

	[Benchmark]
	public async Task SerializeAsync_Normal()
	{
		await _Normal.SerializeAsync(_Models).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task SerializeAsync_Recyclable()
	{
		await _Recyclable.SerializeAsync(_Models).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task DeserializeAsync_Normal()
	{
		await _Normal.DeserializeAsync<List<SampleModel>>(_Blob).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task DeserializeAsync_Recyclable()
	{
		await _Recyclable.DeserializeAsync<List<SampleModel>>(_Blob).ConfigureAwait(false);
	}
}

[MemoryDiagnoser]
[Config(typeof(Config))]
public class SystemTextJsonSerializerBenchmark
	: AbstractSerializersBenchmark
{
	public SystemTextJsonSerializerBenchmark()
	{
		_Normal = new FusionCacheSystemTextJsonSerializer();
		_Recyclable = new FusionCacheSystemTextJsonSerializer(new FusionCacheSystemTextJsonSerializer.Options
		{
			StreamManager = new RecyclableMemoryStreamManager()
		});
	}
}

[MemoryDiagnoser]
[Config(typeof(Config))]
public class ProtobufSerializerBenchmark
	: AbstractSerializersBenchmark
{
	public ProtobufSerializerBenchmark()
	{
		_Normal = new FusionCacheProtoBufNetSerializer();
		_Recyclable = new FusionCacheProtoBufNetSerializer(new FusionCacheProtoBufNetSerializer.Options
		{
			StreamManager = new RecyclableMemoryStreamManager()
		});
	}
}
