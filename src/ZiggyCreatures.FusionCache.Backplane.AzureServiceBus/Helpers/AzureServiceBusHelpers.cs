using System.Text;

namespace ZiggyCreatures.Caching.Fusion.Backplane.AzureServiceBus.Helpers;

/// <summary>
/// Pure helper logic for deriving valid Azure Service Bus entity names (topics/subscriptions) from arbitrary strings, e.g. FusionCache's computed backplane channel name.
/// This is a best-effort sanitizer, not an exhaustive validator against the full Service Bus naming spec.
/// </summary>
internal static class AzureServiceBusHelpers
{
	/// <summary>
	/// The maximum length of a Service Bus topic name.
	/// </summary>
	public const int MaxTopicNameLength = 260;

	/// <summary>
	/// The maximum length of a Service Bus subscription name.
	/// </summary>
	public const int MaxSubscriptionNameLength = 50;

	/// <summary>
	/// Sanitizes <paramref name="name"/> into a valid Service Bus entity name: only letters, digits, '.', '-', '_', '/' are allowed,
	/// the result cannot start/end with a separator, and it is truncated to <paramref name="maxLength"/> characters.
	/// If sanitization removes every character, <paramref name="fallback"/> is returned instead.
	/// </summary>
	public static string SanitizeEntityName(string name, int maxLength, string fallback = "entity")
	{
		if (name is null)
			throw new ArgumentNullException(nameof(name));

		if (maxLength <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxLength));

		var sanitized = new StringBuilder(name.Length);
		foreach (var c in name)
		{
			if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '/')
				sanitized.Append(c);
			else
				sanitized.Append('-');
		}

		var result = sanitized.ToString().Trim('/', '-', '.');

		if (result.Length > maxLength)
			result = result.Substring(0, maxLength).Trim('/', '-', '.');

		return result.Length == 0 ? fallback : result;
	}

	/// <summary>
	/// Resolves the Service Bus topic name to use: <paramref name="explicitTopicName"/> if set (sanitized), otherwise <paramref name="fallback"/> (typically the cache name, also sanitized).
	/// </summary>
	public static string ResolveTopicName(string? explicitTopicName, string fallback)
	{
		var source = string.IsNullOrWhiteSpace(explicitTopicName) ? fallback : explicitTopicName!;

		return SanitizeEntityName(source, MaxTopicNameLength, fallback: "fusioncache-backplane");
	}

	/// <summary>
	/// Generates a unique id, suitable for use as a per-instance subscription name and as the self-message filter value.
	/// Guaranteed to be a valid Service Bus subscription name (max <see cref="AzureServiceBusNaming.MaxSubscriptionNameLength"/> characters, valid character set only).
	/// </summary>
	public static string GenerateId()
	{
		var randomPart = Guid.NewGuid().ToString("N").Substring(0, 6);
		var machineNamePart = SanitizeEntityName(Environment.MachineName, 30, fallback: "machine");

		var id = $"{DateTime.UtcNow:yyMMddHHmmss}-{machineNamePart}-{randomPart}";

		return id.Length >MaxSubscriptionNameLength
			? id.Substring(0, MaxSubscriptionNameLength)
			: id;
	}
}
