using System.Buffers;
using System.IO.Compression;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

/// <summary>
/// A contiguous pooled buffer writer with doubling growth, parameterized by pool,
/// used to compare pooling strategies for the IBufferWriter-based serialization path.
/// </summary>
public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
	private readonly ArrayPool<byte> _pool;
	private byte[] _buffer;
	private int _written;

	public PooledBufferWriter(ArrayPool<byte> pool, int initialCapacity = 4096)
	{
		_pool = pool;
		_buffer = pool.Rent(initialCapacity);
	}

	public int WrittenCount => _written;
	public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);
	public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);
	public ReadOnlySequence<byte> WrittenSequence => new(_buffer, 0, _written);

	public void Advance(int count) => _written += count;

	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		EnsureCapacity(sizeHint);
		return _buffer.AsMemory(_written);
	}

	public Span<byte> GetSpan(int sizeHint = 0)
	{
		EnsureCapacity(sizeHint);
		return _buffer.AsSpan(_written);
	}

	private void EnsureCapacity(int sizeHint)
	{
		if (sizeHint < 1)
			sizeHint = 1;

		if (_written + sizeHint <= _buffer.Length)
			return;

		var newSize = Math.Max(_buffer.Length * 2, _written + sizeHint);
		var newBuffer = _pool.Rent(newSize);
		_buffer.AsSpan(0, _written).CopyTo(newBuffer);
		_pool.Return(_buffer);
		_buffer = newBuffer;
	}

	public byte[] ToArray() => WrittenSpan.ToArray();

	public void Dispose()
	{
		_pool.Return(_buffer);
		_buffer = [];
		_written = 0;
	}
}

/// <summary>
/// A segmented pooled buffer writer: rents fixed-size chunks, never copies on growth,
/// produces a (potentially) multi-segment ReadOnlySequence.
/// </summary>
public sealed class SegmentedPooledBufferWriter : IBufferWriter<byte>, IDisposable
{
	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		public Segment(ReadOnlyMemory<byte> memory, Segment? previous)
		{
			Memory = memory;
			if (previous is not null)
			{
				RunningIndex = previous.RunningIndex + previous.Memory.Length;
				previous.Next = this;
			}
		}
	}

	private readonly ArrayPool<byte> _pool;
	private readonly int _chunkSize;
	private readonly List<byte[]> _chunks = [];
	private byte[] _current;
	private int _writtenInCurrent;
	private long _totalWritten;

	public SegmentedPooledBufferWriter(ArrayPool<byte> pool, int chunkSize = 64 * 1024)
	{
		_pool = pool;
		_chunkSize = chunkSize;
		_current = pool.Rent(chunkSize);
		_chunks.Add(_current);
	}

	public long WrittenCount => _totalWritten;

	public void Advance(int count)
	{
		_writtenInCurrent += count;
		_totalWritten += count;
	}

	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		EnsureCapacity(sizeHint);
		return _current.AsMemory(_writtenInCurrent);
	}

	public Span<byte> GetSpan(int sizeHint = 0)
	{
		EnsureCapacity(sizeHint);
		return _current.AsSpan(_writtenInCurrent);
	}

	private void EnsureCapacity(int sizeHint)
	{
		if (sizeHint < 1)
			sizeHint = 1;

		if (_writtenInCurrent + sizeHint <= _current.Length)
			return;

		_current = _pool.Rent(Math.Max(_chunkSize, sizeHint));
		_chunks.Add(_current);
		_writtenInCurrent = 0;
	}

	public ReadOnlySequence<byte> GetWrittenSequence()
	{
		if (_chunks.Count == 1)
			return new ReadOnlySequence<byte>(_current, 0, _writtenInCurrent);

		Segment? first = null;
		Segment? last = null;
		for (var i = 0; i < _chunks.Count; i++)
		{
			var chunk = _chunks[i];
			var length = i == _chunks.Count - 1 ? _writtenInCurrent : chunk.Length;
			last = new Segment(chunk.AsMemory(0, length), last);
			first ??= last;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
	}

	public void Dispose()
	{
		foreach (var chunk in _chunks)
		{
			_pool.Return(chunk);
		}
		_chunks.Clear();
		_totalWritten = 0;
		_writtenInCurrent = 0;
		_current = [];
	}
}

[Config(typeof(Config))]
public class BufferWriterExperiments
{
	public class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
			AddDiagnoser(MemoryDiagnoser.Default);
			AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams);
			AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));
			WithOrderer(new DefaultOrderer(summaryOrderPolicy: SummaryOrderPolicy.Declared));
		}
	}

	// MIMICS THE CURRENT ArrayPoolBufferWriter POOL (ArrayPool<byte>.Create(): 1MB MAX ARRAY SIZE)
	private static readonly ArrayPool<byte> _privatePool = ArrayPool<byte>.Create();

	// THE "HEAVY-HANDED CONFIG" OPTION: A PRIVATE POOL WITH A MUCH BIGGER MAX ARRAY SIZE.
	// NOTE: ConfigurableArrayPool NEVER TRIMS, SO RETAINED ARRAYS (UP TO 64MB x 8 IN THE TOP BUCKET ALONE) ARE HELD FOREVER.
	private static readonly ArrayPool<byte> _bigPrivatePool = ArrayPool<byte>.Create(64 * 1024 * 1024, 8);

	// 10 -> ~1.4KB, 700 -> ~100KB, 30_000 -> ~4.3MB (VERIFIED IN SETUP OUTPUT)
	[Params(10, 700, 30_000)]
	public int ModelCount;

	private List<SampleModel> _models = [];
	private byte[] _blob = null!;
	private ReadOnlySequence<byte> _multiSegmentBlob;
	private byte[] _compressedBlob = null!;

	[GlobalSetup]
	public void Setup()
	{
		_models = [];
		for (var i = 0; i < ModelCount; i++)
		{
			_models.Add(SampleModel.GenerateRandom());
		}

		_blob = JsonSerializer.SerializeToUtf8Bytes(_models);
		Console.WriteLine($"### ModelCount={ModelCount} -> payload {_blob.Length:N0} bytes");

		// SIMULATES A CHUNK-FILLED BUFFER (E.G. A SEGMENTED WRITER, OR A CHUNKED COPY FROM THE NETWORK)
		_multiSegmentBlob = BuildMultiSegmentSequence(_blob, 16 * 1024);

		using var output = new MemoryStream();
		using (var brotli = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
		{
			JsonSerializer.Serialize(brotli, _models);
		}
		_compressedBlob = output.ToArray();
	}

	private static ReadOnlySequence<byte> BuildMultiSegmentSequence(byte[] data, int segmentSize)
	{
		var segments = new List<ReadOnlyMemory<byte>>();
		for (var offset = 0; offset < data.Length; offset += segmentSize)
		{
			segments.Add(data.AsMemory(offset, Math.Min(segmentSize, data.Length - offset)));
		}

		if (segments.Count == 1)
			return new ReadOnlySequence<byte>(segments[0]);

		ChainSegment? first = null;
		ChainSegment? last = null;
		foreach (var segment in segments)
		{
			last = new ChainSegment(segment, last);
			first ??= last;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
	}

	private sealed class ChainSegment : ReadOnlySequenceSegment<byte>
	{
		public ChainSegment(ReadOnlyMemory<byte> memory, ChainSegment? previous)
		{
			Memory = memory;
			if (previous is not null)
			{
				RunningIndex = previous.RunningIndex + previous.Memory.Length;
				previous.Next = this;
			}
		}
	}

	// ---------------------------------------------------------------
	// Q1/Q2: SERIALIZE - STATUS QUO byte[] VS POOLED WRITER VARIANTS
	// ---------------------------------------------------------------

	[Benchmark(Baseline = true)]
	public int Serialize_ByteArray()
	{
		return JsonSerializer.SerializeToUtf8Bytes(_models).Length;
	}

	[Benchmark]
	public int Serialize_PooledWriter_PrivatePool()
	{
		using var writer = new PooledBufferWriter(_privatePool);
		using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { SkipValidation = true });
		JsonSerializer.Serialize(jsonWriter, _models);
		return writer.WrittenCount;
	}

	[Benchmark]
	public int Serialize_PooledWriter_BigPrivatePool()
	{
		using var writer = new PooledBufferWriter(_bigPrivatePool);
		using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { SkipValidation = true });
		JsonSerializer.Serialize(jsonWriter, _models);
		return writer.WrittenCount;
	}

	[Benchmark]
	public int Serialize_PooledWriter_SharedPool()
	{
		using var writer = new PooledBufferWriter(ArrayPool<byte>.Shared);
		using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { SkipValidation = true });
		JsonSerializer.Serialize(jsonWriter, _models);
		return writer.WrittenCount;
	}

	[ThreadStatic]
	private static Utf8JsonWriter? _cachedUtf8JsonWriter;

	[Benchmark]
	public int Serialize_PooledWriter_CachedJsonWriter()
	{
		using var writer = new PooledBufferWriter(ArrayPool<byte>.Shared);
		var jsonWriter = _cachedUtf8JsonWriter;
		if (jsonWriter is null)
		{
			jsonWriter = _cachedUtf8JsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { SkipValidation = true });
		}
		else
		{
			jsonWriter.Reset(writer);
		}
		JsonSerializer.Serialize(jsonWriter, _models);
		return writer.WrittenCount;
	}

	[Benchmark]
	public long Serialize_SegmentedWriter()
	{
		using var writer = new SegmentedPooledBufferWriter(ArrayPool<byte>.Shared);
		using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { SkipValidation = true });
		JsonSerializer.Serialize(jsonWriter, _models);
		return writer.WrittenCount;
	}

	[Benchmark]
	public long Serialize_SegmentedWriter_ThenLinearize()
	{
		// WHAT REDIS' IBufferDistributedCache.Set DOES WITH A MULTI-SEGMENT SEQUENCE:
		// RENTS AND COPIES (RedisCache.Linearize)
		using var writer = new SegmentedPooledBufferWriter(ArrayPool<byte>.Shared);
		using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { SkipValidation = true });
		JsonSerializer.Serialize(jsonWriter, _models);

		var sequence = writer.GetWrittenSequence();
		if (sequence.IsSingleSegment)
			return sequence.First.Length;

		var length = checked((int)sequence.Length);
		var lease = ArrayPool<byte>.Shared.Rent(length);
		sequence.CopyTo(lease);
		ArrayPool<byte>.Shared.Return(lease);
		return length;
	}

	// ---------------------------------------------------------------
	// Q3: DESERIALIZE - byte[] VS SINGLE-SEGMENT VS MULTI-SEGMENT
	// ---------------------------------------------------------------

	[Benchmark]
	public int Deserialize_ByteArray()
	{
		return JsonSerializer.Deserialize<List<SampleModel>>(_blob)!.Count;
	}

	[Benchmark]
	public int Deserialize_SingleSegmentSequence()
	{
		var sequence = new ReadOnlySequence<byte>(_blob);
		if (sequence.IsSingleSegment)
		{
			return JsonSerializer.Deserialize<List<SampleModel>>(sequence.First.Span)!.Count;
		}

		var reader = new Utf8JsonReader(sequence);
		return JsonSerializer.Deserialize<List<SampleModel>>(ref reader)!.Count;
	}

	[Benchmark]
	public int Deserialize_MultiSegmentSequence16K()
	{
		var reader = new Utf8JsonReader(_multiSegmentBlob);
		return JsonSerializer.Deserialize<List<SampleModel>>(ref reader)!.Count;
	}

	// ---------------------------------------------------------------
	// Q4: BROTLI (#528) - GIST-STYLE byte[] PIPELINE VS FULLY BUFFERED
	// ---------------------------------------------------------------

	[Benchmark]
	public int Brotli_Serialize_Gist()
	{
		using var output = new MemoryStream();
		using (var brotli = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
		{
			JsonSerializer.Serialize(brotli, _models);
		}
		return output.ToArray().Length;
	}

	[Benchmark]
	public int Brotli_Serialize_Buffered()
	{
		// STAGE 1: JSON -> POOLED BUFFER
		using var jsonBuffer = new PooledBufferWriter(ArrayPool<byte>.Shared);
		using (var jsonWriter = new Utf8JsonWriter(jsonBuffer, new JsonWriterOptions { SkipValidation = true }))
		{
			JsonSerializer.Serialize(jsonWriter, _models);
		}

		// STAGE 2: BROTLI COMPRESS SPAN -> POOLED BUFFER (SINGLE SHOT)
		using var compressed = new PooledBufferWriter(ArrayPool<byte>.Shared);
		var source = jsonBuffer.WrittenSpan;
		var destination = compressed.GetSpan(BrotliEncoder.GetMaxCompressedLength(source.Length));
		if (!BrotliEncoder.TryCompress(source, destination, out var written, quality: 1, window: 22))
			throw new InvalidOperationException("Brotli compression failed.");
		compressed.Advance(written);

		// RESULT WOULD FLOW TO IBufferDistributedCache.Set AS A ReadOnlySequence - NO FINAL ARRAY
		return compressed.WrittenCount;
	}

	[Benchmark]
	public int Brotli_Deserialize_Gist()
	{
		using var input = new MemoryStream(_compressedBlob);
		using var brotli = new BrotliStream(input, CompressionMode.Decompress);
		return JsonSerializer.Deserialize<List<SampleModel>>(brotli)!.Count;
	}

	[Benchmark]
	public int Brotli_Deserialize_Buffered()
	{
		// STAGE 1: BROTLI DECOMPRESS SPAN -> POOLED BUFFER (CHUNKED, OUTPUT SIZE UNKNOWN)
		using var decompressed = new PooledBufferWriter(ArrayPool<byte>.Shared, _compressedBlob.Length * 4);
		var decoder = new BrotliDecoder();
		ReadOnlySpan<byte> source = _compressedBlob;
		while (true)
		{
			var destination = decompressed.GetSpan(64 * 1024);
			var status = decoder.Decompress(source, destination, out var consumed, out var written);
			decompressed.Advance(written);
			source = source[consumed..];

			if (status == OperationStatus.Done)
				break;
			if (status == OperationStatus.InvalidData)
				throw new InvalidOperationException("Brotli decompression failed.");
		}

		// STAGE 2: JSON DESERIALIZE FROM THE POOLED SPAN
		return JsonSerializer.Deserialize<List<SampleModel>>(decompressed.WrittenSpan)!.Count;
	}
}
