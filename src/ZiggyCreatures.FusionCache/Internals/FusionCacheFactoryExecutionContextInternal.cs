using System;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	internal class FusionCacheFactoryExecutionContextInternal<T>
		: FusionCacheFactoryExecutionContext
	{
		private readonly MaybeValue<T> _staleValue;

		public FusionCacheFactoryExecutionContextInternal(FusionCacheEntryOptions options, DateTimeOffset? lastModified, string? etag, MaybeValue<T> staleValue)
			: base(options, lastModified, etag)
		{
			_staleValue = staleValue;
		}

		public override MaybeValue<TValue> TryGetStaleValue<TValue>()
		{
			return _staleValue.HasValue == false
				? MaybeValue<TValue>.None
				: MaybeValue<TValue>.FromValue((TValue)(object)_staleValue.Value)
			;
		}
	}
}
