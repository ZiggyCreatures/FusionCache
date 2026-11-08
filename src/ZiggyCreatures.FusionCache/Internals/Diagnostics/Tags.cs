namespace ZiggyCreatures.Caching.Fusion.Internals.Diagnostics;

internal static class Tags
{
	internal static class Names
	{
		public const string CacheName = "fusioncache.cache.name";
		public const string CacheInstanceId = "fusioncache.cache.instance_id";

		public const string OperationKey = "fusioncache.operation.key";
		public const string OperationId = "fusioncache.operation.operation_id";
		public const string OperationTag = "fusioncache.operation.tag";
		public const string OperationLevel = "fusioncache.operation.level";
		public const string OperationBackground = "fusioncache.operation.background";

		public const string Hit = "fusioncache.hit";
		public const string Stale = "fusioncache.stale";

		public const string FactoryEagerRefresh = "fusioncache.factory.eager_refresh";


		public const string MemoryEvictReason = "fusioncache.memory.evict_reason";

		public const string DistributedCircuitBreakerClosed = "fusioncache.distributed.circuit_breaker.closed";

		public const string BackplaneCircuitBreakerClosed = "fusioncache.backplane.circuit_breaker.closed";
		public const string BackplaneMessageAction = "fusioncache.backplane.message_action";
		public const string BackplaneMessageSourceId = "fusioncache.backplane.message_source_id";
	}

	// NOTE: PRE-BOXED bool VALUES: TAG VALUES ARE object?, SO PASSING A bool DIRECTLY WOULD BOX
	// ON EVERY SINGLE INSTRUMENTED OPERATION. THERE ARE ONLY TWO POSSIBLE VALUES, SO CACHE THEM.
	private static readonly object BoxedTrue = true;
	private static readonly object BoxedFalse = false;

	internal static KeyValuePair<string, object?> Tag(string name, bool value)
	{
		return new KeyValuePair<string, object?>(name, value ? BoxedTrue : BoxedFalse);
	}

	internal static KeyValuePair<string, object?> Tag(string name, string? value)
	{
		return new KeyValuePair<string, object?>(name, value);
	}
}
