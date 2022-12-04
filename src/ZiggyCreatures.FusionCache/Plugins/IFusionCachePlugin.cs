namespace ZiggyCreatures.Caching.Fusion.Plugins;

/// <summary>
/// The core plugin interface to implement to create a FusionCache plugin.
/// </summary>
public interface IFusionCachePlugin
{
	/// <summary>
	/// This method is called right after adding the plugin to a FusionCache instance. If it throws, the plugin will be automatically removed.
	/// </summary>
	/// <param name="cache">The FusionCache instance on which to operate.</param>
	void Start(IFusionCache cache);

	/// <summary>
	/// This method is called right before removing the plugin from a FusionCache instance.
	/// </summary>
	/// <param name="cache">The FusionCache instance on which to operate.</param>
	void Stop(IFusionCache cache);
}
