using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace FusionCacheTests;

public sealed class L1L2Tests_Buffer(ITestOutputHelper output) : L1L2Tests(output)
{
	protected override IDistributedCache CreateDistributedCache()
	{
		return new BufferMemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
	}

	private sealed class BufferMemoryDistributedCache(IOptions<MemoryDistributedCacheOptions> optionsAccessor)
		: MemoryDistributedCache(optionsAccessor), IBufferDistributedCache
	{
		public bool TryGet(string key, IBufferWriter<byte> destination)
		{
			var bytes = Get(key);
			if (bytes is null)
			{
				return false;
			}

			destination.Write(bytes);
			return true;
		}

		public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, CancellationToken token)
		{
			var bytes = await GetAsync(key, token);
			if (bytes is null)
			{
				return false;
			}

			destination.Write(bytes);
			return true;
		}

		public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options)
		{
			Set(key, value.ToArray(), options);
		}

		public ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions options, CancellationToken token)
		{
			return new ValueTask(SetAsync(key, value.ToArray(), options, token));
		}
	}
}
