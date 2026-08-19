using FusionCacheTests.Stuff;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace FusionCacheTests;

public class SimpleCircuitBreakerTests
{
	[Fact]
	public void ZeroBreakDuration_TryOpen_IsNoOp()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.Zero);

		var opened = cb.TryOpen(out var openChanged);
		var closed = cb.IsClosed(out var closeChanged);

		Assert.False(opened);
		Assert.False(openChanged);
		Assert.True(closed);
		Assert.False(closeChanged);
	}

	[Fact]
	public void TryOpen_FirstTime_ReportsStateChange()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.FromSeconds(1));

		var opened = cb.TryOpen(out var stateChanged);

		Assert.True(opened);
		Assert.True(stateChanged);
	}

	[Fact]
	public void TryOpen_WhileAlreadyOpen_ReportsNoStateChange()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.FromSeconds(1));
		cb.TryOpen(out _);

		var opened = cb.TryOpen(out var stateChanged);

		Assert.True(opened);
		Assert.False(stateChanged);
	}

	[Fact]
	public void IsClosed_BeforeFirstOpen_ReturnsTrue()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.FromSeconds(1));

		var closed = cb.IsClosed(out var stateChanged);

		Assert.True(closed);
		Assert.False(stateChanged);
	}

	[Fact]
	public void IsClosed_WithinBreakDuration_ReturnsFalse()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.FromSeconds(10));
		cb.TryOpen(out _);

		var closed = cb.IsClosed(out var stateChanged);

		Assert.False(closed);
		Assert.False(stateChanged);
	}

	[Fact]
	public async Task IsClosed_AfterBreakDurationElapses_ReturnsTrueAndReportsStateChange()
	{
		var breakDuration = TimeSpan.FromSeconds(1);
		var cb = new SimpleCircuitBreaker(breakDuration);
		cb.TryOpen(out _);

		await Task.Delay(breakDuration.PlusASecond(), TestContext.Current.CancellationToken);

		var closed = cb.IsClosed(out var stateChanged);

		Assert.True(closed);
		Assert.True(stateChanged);
	}

	[Fact]
	public async Task IsClosed_AfterBreakDurationElapses_SubsequentCallReportsNoStateChange()
	{
		var breakDuration = TimeSpan.FromSeconds(1);
		var cb = new SimpleCircuitBreaker(breakDuration);
		cb.TryOpen(out _);
		await Task.Delay(breakDuration.PlusASecond(), TestContext.Current.CancellationToken);
		cb.IsClosed(out _);

		var closed = cb.IsClosed(out var stateChanged);

		Assert.True(closed);
		Assert.False(stateChanged);
	}

	[Fact]
	public void Close_AfterTryOpen_ReportsStateChange()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.FromSeconds(1));
		cb.TryOpen(out _);

		cb.Close(out var stateChanged);

		Assert.True(stateChanged);
		Assert.True(cb.IsClosed(out _));
	}

	[Fact]
	public void Close_WhenAlreadyClosed_ReportsNoStateChange()
	{
		var cb = new SimpleCircuitBreaker(TimeSpan.FromSeconds(1));

		cb.Close(out var stateChanged);

		Assert.False(stateChanged);
	}

	[Fact]
	public async Task TryOpen_ResetsBreakWindow()
	{
		var breakDuration = TimeSpan.FromSeconds(2);
		var cb = new SimpleCircuitBreaker(breakDuration);
		cb.TryOpen(out _);

		await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
		// re-open: the window restarts from "now"
		cb.TryOpen(out _);

		// ~1s into the new 2s window — the original window would already be expired without the reset
		await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
		Assert.False(cb.IsClosed(out _));

		await Task.Delay(breakDuration.PlusASecond(), TestContext.Current.CancellationToken);
		Assert.True(cb.IsClosed(out _));
	}
}
