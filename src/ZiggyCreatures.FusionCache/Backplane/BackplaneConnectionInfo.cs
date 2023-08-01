namespace ZiggyCreatures.Caching.Fusion.Backplane
{
	/// <summary>
	/// A struct containing information about a backplane connection or re-connection.
	/// </summary>
	public readonly struct BackplaneConnectionInfo
	{
		/// <summary>
		/// Creates a new <see cref="BackplaneConnectionInfo"/> instance.
		/// </summary>
		/// <param name="isReconnection"></param>
		public BackplaneConnectionInfo(bool isReconnection)
		{
			IsReconnection = isReconnection;
		}

		/// <summary>
		/// If set to <see langword="true"/>, the connection is a re-connection.
		/// </summary>
		public readonly bool IsReconnection { get; }
	}
}
