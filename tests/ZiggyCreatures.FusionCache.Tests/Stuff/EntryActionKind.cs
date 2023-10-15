namespace FusionCacheTests.Stuff
{
	public enum EntryActionKind
	{
		Miss = 0,
		HitNormal = 1,
		HitStale = 2,
		Set = 3,
		Remove = 4,
		FailSafeActivate = 5,
		FactoryError = 6,
		FactorySuccess = 7,
		BackplaneMessagePublished = 8,
		BackplaneMessageReceived = 9
	}
}
