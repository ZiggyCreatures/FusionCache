using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace ZiggyCreatures.Caching.Fusion.Internals
{
	internal static class EncodingExtensions
	{
#if NETSTANDARD2_0
		public static int GetBytes<T>(this T encoding, string s, Span<byte> span) where T : Encoding
		{
			int byteCount = encoding.GetByteCount(s);
			byte[] stringBytes = ArrayPool<byte>.Shared.Rent(byteCount);
			try
			{
				encoding.GetBytes(s, 0, s.Length, stringBytes, 0);
				stringBytes.AsSpan(0, byteCount).CopyTo(span);
				return byteCount;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(stringBytes);
			}
		}

		public static string GetString<T>(this T encoding, ReadOnlySpan<byte> bytes) where T : Encoding
		{
			byte[] stringBytes = ArrayPool<byte>.Shared.Rent(bytes.Length);
			try
			{
				bytes.CopyTo(stringBytes);
				return encoding.GetString(stringBytes, 0, bytes.Length);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(stringBytes);
			}
		}
#endif
	}
}
