using System.Linq;

namespace ZiggyCreatures.Caching.Fusion
{
	/// <summary>
	/// A default implementation of <see cref="IKeyedFusionCacheEntryOptionsProvider"/> that provides <see cref="FusionCacheEntryOptions"/> based on the key prefix.
	/// </summary>
	public class KeyPrefixBasedEntryOptionsProvider : IKeyedFusionCacheEntryOptionsProvider
	{
		private readonly PrefixLookup<FusionCacheEntryOptions> _keyDependentCacheEntryOptionsLookup;

		/// <summary>
		/// Creates the <see cref="KeyPrefixBasedEntryOptionsProvider"/> instance.
		/// </summary>
		/// <param name="options">An instance of <see cref="FusionCacheOptions"/>.</param>
		public KeyPrefixBasedEntryOptionsProvider(FusionCacheOptions options)
		{
			_keyDependentCacheEntryOptionsLookup = new PrefixLookup<FusionCacheEntryOptions>(
				options.KeyDependentEntryOptions.ToDictionary(
					entry => entry.KeyTemplate,
					entry => entry.Options)!);
		}

		/// <inheritdoc />
		public FusionCacheEntryOptions? GetEntryOptions(string key) => 
			_keyDependentCacheEntryOptionsLookup.TryFind(key);
	}
}
