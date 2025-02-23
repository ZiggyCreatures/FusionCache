namespace FusionCacheTests.Stuff;

internal class SimpleDisposable
	: IDisposable
{

	public bool IsDisposed { get; private set; }

	public void Dispose()
	{
		IsDisposed = true;
	}
}
