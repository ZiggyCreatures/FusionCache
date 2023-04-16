using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MemoryPack;

namespace FusionCacheTests
{
	[DataContract]
	[MemoryPackable]
	public partial class ComplexType : IEquatable<ComplexType?>
	{
		[DataMember(Name = "pi1", Order = 1)]
		public int PropInt { get; set; }
		[DataMember(Name = "pi2", Order = 2)]
		public int? PropIntNullable { get; set; }
		[DataMember(Name = "ps", Order = 3)]
		public string? PropString { get; set; }
		[DataMember(Name = "pb", Order = 4)]
		public bool PropBool { get; set; }

		public override bool Equals(object? obj)
		{
			return Equals(obj as ComplexType);
		}

		public bool Equals(ComplexType? other)
		{
			return other is not null &&
				   PropInt == other.PropInt &&
				   PropIntNullable == other.PropIntNullable &&
				   PropString == other.PropString &&
				   PropBool == other.PropBool;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(PropInt, PropIntNullable, PropString, PropBool);
		}

		public static bool operator ==(ComplexType? left, ComplexType? right)
		{
			return EqualityComparer<ComplexType>.Default.Equals(left, right);
		}

		public static bool operator !=(ComplexType? left, ComplexType? right)
		{
			return !(left == right);
		}

		public static ComplexType CreateSample()
		{
			return new ComplexType
			{
				PropInt = 42,
				PropIntNullable = null,
				PropString = "sloths!",
				PropBool = true
			};
		}
	}
}
