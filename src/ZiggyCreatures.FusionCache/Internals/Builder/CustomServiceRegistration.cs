using System;

namespace ZiggyCreatures.Caching.Fusion.Internals.Builder
{
	/// <summary>
	/// Represents a custom service lookup configuration for a specific service type.
	/// </summary>
	/// <typeparam name="T">Type (normally, interface) of the service lookup.</typeparam>
	public class CustomServiceRegistration<T>
		where T : class
	{
		/// <summary>
		/// Indicates if the builder should try find and use a service registered in the DI container.
		/// </summary>
		public bool UseRegistered { get; set; } = true;

		/// <summary>
		/// The keyed service key to use for DI lookup.
		/// </summary>
		public object? ServiceKey { get; set; }

		/// <summary>
		/// A specific service instance to be used.
		/// </summary>
		public T? Instance { get; set; }

		/// <summary>
		/// A factory that creates the service instance to be used.
		/// </summary>
		public Func<IServiceProvider, T>? InstanceFactory { get; set; }

		/// <summary>
		/// Throws an <see cref="InvalidOperationException"/> if an instance of the service is not specified or is not found in the DI container.
		/// </summary>
		public bool ThrowIfMissing { get; set; }
	}
}
