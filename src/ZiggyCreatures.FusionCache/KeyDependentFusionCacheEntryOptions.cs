namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// Represents all the options available for a single <see cref="IFusionCache"/> entry
	/// for a cache key matching the template.
	/// <br/><br/>
	/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
	/// </summary>
	public sealed class KeyDependentFusionCacheEntryOptions
	{
		/// <summary>
		/// Gets or sets the key template, for which the <see cref="FusionCacheEntryOptions"/> are to be used.
		/// Any given cache key will be checked for a prefix matching this template.
		/// <br/><br/>
		/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
		/// </summary>
		public string KeyTemplate { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="FusionCacheEntryOptions"/> to use when the cache key matches the key template.
		/// <br/><br/>
		/// <strong>DOCS:</strong> <see href="https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Options.md"/>
		/// </summary>
		public FusionCacheEntryOptions Options { get; set; }

		public KeyDependentFusionCacheEntryOptions Duplicate() =>
			new KeyDependentFusionCacheEntryOptions
			{
				KeyTemplate = KeyTemplate, 
				Options = Options.Duplicate()
			};
	}
}
