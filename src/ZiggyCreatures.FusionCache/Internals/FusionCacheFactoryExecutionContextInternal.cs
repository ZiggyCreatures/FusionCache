using System;
using System.Collections.Generic;
using System.Text;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	internal class FusionCacheFactoryExecutionContextInternal<T>
		: FusionCacheFactoryExecutionContext
	{
		private readonly MaybeValue<T> _staleValue;

		public FusionCacheFactoryExecutionContextInternal(FusionCacheEntryOptions options, MaybeValue<T> staleValue)
			: base(options)
		{
			_staleValue = staleValue;
		}

		public override MaybeValue<TValue> TryGetStaleValue<TValue>()
		{
			return
				_staleValue.HasValue == false
				? MaybeValue<TValue>.None
				: MaybeValue<TValue>.FromValue((TValue)(object)_staleValue.Value)
			;
		}
	}
}
