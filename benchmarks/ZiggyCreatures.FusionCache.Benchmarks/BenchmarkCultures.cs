using System.Globalization;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

internal static class BenchmarkCultures
{
	public static CultureInfo Create(string name)
	{
		if (name == "Invariant")
			return CultureInfo.InvariantCulture;

		try
		{
			return CultureInfo.GetCultureInfo(name);
		}
		catch (CultureNotFoundException)
		{
			// INVARIANT GLOBALIZATION MODE (PredefinedCulturesOnly)
			return CultureInfo.InvariantCulture;
		}
	}
}
