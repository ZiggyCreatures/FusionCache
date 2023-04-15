using System;

namespace ZiggyCreatures.Caching.Fusion;

/// <summary>
/// Models the execution context passed to a FusionCache factory. Right now it just contains the options so they can be modified based of the factory execution (see adaptive caching), but in the future this may contain more.
/// </summary>
public abstract class FusionCacheFactoryExecutionContext
{
	private FusionCacheEntryOptions _options;

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="options">The options to start from.</param>
	public FusionCacheFactoryExecutionContext(FusionCacheEntryOptions options)
	{
		_options = options;
	}

	/// <summary>
	/// The options currently used, and that can be modified or changed completely.
	/// </summary>
	public FusionCacheEntryOptions Options
	{
		get
		{
			if (_options is null)
			{
#pragma warning disable CS8603 // Possible null reference return.
				return null;
#pragma warning restore CS8603 // Possible null reference return.
			}

			if (_options.IsSafeForAdaptiveCaching == false)
			{
				_options = _options.EnsureIsSafeForAdaptiveCaching();
			}

			return _options;
		}
		set
		{
#pragma warning disable CS8601 // Possible null reference assignment.
			_options = value?.SetIsSafeForAdaptiveCaching();
#pragma warning restore CS8601 // Possible null reference assignment.
		}
	}

	internal FusionCacheEntryOptions GetOptions()
	{
		return _options;
	}

	public abstract MaybeValue<TValue> TryGetStaleValue<TValue>();
}
