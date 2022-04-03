namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// Models the execution context passed to a FusionCache factory. Right now it just contains the options so they can be modified based of the factory execution (see adaptive caching), but in the future this may contain more.
	/// </summary>
	public class FusionCacheFactoryExecutionContext
	{
		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="options">The options to start from.</param>
		public FusionCacheFactoryExecutionContext(FusionCacheEntryOptions options)
		{
			Options = options;
		}

		/// <summary>
		/// The options currently used, and that can be modified or changed completely.
		/// </summary>
		public FusionCacheEntryOptions Options { get; set; }
	}
}
