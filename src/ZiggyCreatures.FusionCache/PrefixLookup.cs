using System;
using System.Collections.Generic;
using System.Linq;

namespace ZiggyCreatures.Caching.Fusion
{
	internal sealed class PrefixLookup<T>
		where T : class
	{
		private readonly string[] _prefixes;
		private readonly T?[] _values;

		public PrefixLookup(IReadOnlyDictionary<string, T?> values)
		{
			if (values.Count == 0)
			{
				_prefixes = [];
				_values = [];
				return;
			}

			_prefixes = new string[values.Count];
			_values = new T[values.Count];
			var i = 0;

			foreach(var item in values.OrderBy(pair => pair.Key))
			{
				_prefixes[i] = item.Key;
				_values[i] = item.Value;
				i++;
			}
		}

		public T? TryFind(string key)
		{
			if (_prefixes.Length == 0)
				return null;

			var result = Array.BinarySearch(_prefixes, key);
			if (result < 0)
			{
				result = (~result) - 1;
			}
			
			return result >= 0 && key.StartsWith(_prefixes[result])
				? _values[result]
				: null;
		}
	}
}
