using System;
using System.Collections.Generic;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

public class SamplePayload : IEquatable<SamplePayload?>
{
	public SamplePayload()
	{
		Foo = "foo";
		Bar = "bar";
		Baz = 42;
	}

	public string Foo { get; set; }
	public string Bar { get; set; }
	public int Baz { get; set; }

	public override bool Equals(object? obj)
	{
		return Equals(obj as SamplePayload);
	}

	public bool Equals(SamplePayload? other)
	{
		return other is not null &&
			   Foo == other.Foo &&
			   Bar == other.Bar &&
			   Baz == other.Baz;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Foo, Bar, Baz);
	}

	public static bool operator ==(SamplePayload? left, SamplePayload? right)
	{
		return EqualityComparer<SamplePayload>.Default.Equals(left, right);
	}

	public static bool operator !=(SamplePayload? left, SamplePayload? right)
	{
		return !(left == right);
	}
}
