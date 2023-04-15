using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using MemoryPack;

namespace FusionCacheTests
{
	[DataContract]
	[MemoryPackable]
	public partial class ComplexType
	{
		[DataMember(Name = "pi", Order = 1)]
		public int PropInt { get; set; }
		[DataMember(Name = "ps", Order = 2)]
		public string? PropString { get; set; }
		[DataMember(Name = "pb", Order = 3)]
		public bool PropBool { get; set; }

		public override bool Equals(object? obj)
		{
			return obj is ComplexType type &&
				   PropInt == type.PropInt &&
				   PropString == type.PropString &&
				   PropBool == type.PropBool;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(PropInt, PropString, PropBool);
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
				PropString = "sloths!",
				PropBool = true
			};
		}
	}
}
