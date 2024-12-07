namespace ZiggyCreatures.Caching.Fusion.Internals
{
	// SOURCE: https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.Caching.Hybrid/Internal/ImmutableTypeCache.T.cs
	// COPIED (ALMOST) AS-IS FOR MAXIMUM COMPATIBILITY WITH HybridCache
	internal static class ImmutableTypeCache<T> // lazy memoize; T doesn't change per cache instance
	{
		// note for blittable types: a pure struct will be a full copy every time - nothing shared to mutate
		public static readonly bool IsImmutable = /*(typeof(T).IsValueType && ImmutableTypeCache.IsBlittable<T>()) ||*/ FusionCacheInternalUtils.IsTypeImmutable(typeof(T));
	}
}
