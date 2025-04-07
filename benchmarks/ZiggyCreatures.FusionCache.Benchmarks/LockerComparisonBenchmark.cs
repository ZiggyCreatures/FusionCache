using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using ZiggyCreatures.Caching.Fusion.Locking;
using ZiggyCreatures.Caching.Fusion.Locking.AsyncKeyed;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class LockerComparisonBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
			AddDiagnoser(MemoryDiagnoser.Default);
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
			WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default.WithMaxParameterColumnWidth(50));
		}
	}


	[Params(200, 1_000)]
	public int NumberOfLocks;

	[Params(200, 1_000)]
	public int Contention;

	[Params(0, 1, 5)]
	public int GuidReversals;

	private readonly Dictionary<int, List<int>> _shuffledIntegers = new();

	private StandardMemoryLocker _StandardMemoryLocker = null!;
	private ParallelQuery<Task> _StandardMemoryLockerTasks = null!;

	private ProbabilisticMemoryLocker _ProbabilisticMemoryLocker = null!;
	private ParallelQuery<Task> _ProbabilisticMemoryLockerTasks = null!;

	private ExperimentalMemoryLocker _ExperimentalMemoryLocker = null!;
	private ParallelQuery<Task> _ExperimentalMemoryLockerTasks = null!;

	private AsyncKeyedMemoryLocker _AsyncKeyedMemoryLocker = null!;
	private ParallelQuery<Task> _AsyncKeyedMemoryLockerTasks = null!;

	private AsyncKeyedMemoryLocker2 _AsyncKeyedMemoryLocker2 = null!;
	private ParallelQuery<Task> _AsyncKeyedMemoryLocker2Tasks = null!;

	private StripedAsyncKeyedMemoryLocker _StripedAsyncKeyedMemoryLocker = null!;
	private ParallelQuery<Task> _StripedAsyncKeyedMemoryLockerTasks = null!;

	[GlobalSetup]
	public void Setup()
	{
		_StandardMemoryLocker = new StandardMemoryLocker();
		_ProbabilisticMemoryLocker = new ProbabilisticMemoryLocker();
		_ExperimentalMemoryLocker = new ExperimentalMemoryLocker();
		_AsyncKeyedMemoryLocker = new AsyncKeyedMemoryLocker();
		_AsyncKeyedMemoryLocker2 = new AsyncKeyedMemoryLocker2();
		_StripedAsyncKeyedMemoryLocker = new StripedAsyncKeyedMemoryLocker();
	}

	[IterationSetup]
	public void IterationSetup()
	{
		if (!_shuffledIntegers.TryGetValue(Contention * NumberOfLocks, out var _shuffledIntegerList))
		{
			_shuffledIntegerList = Enumerable.Range(0, Contention * NumberOfLocks).ToList();
			Shuffle(_shuffledIntegerList);
			_shuffledIntegers[Contention * NumberOfLocks] = _shuffledIntegerList;
		}

		_StandardMemoryLockerTasks = _shuffledIntegerList
					.Select(async i =>
					{
						var key = (i % NumberOfLocks).ToString();

						var mylock = await _StandardMemoryLocker.AcquireLockAsync(null, null, null, key, TimeSpan.FromSeconds(5), null, default).ConfigureAwait(false);
						Operation();
						_StandardMemoryLocker.ReleaseLock(null, null, null, key, mylock, null);
					}).ToList().AsParallel();

		_ProbabilisticMemoryLockerTasks = _shuffledIntegerList
			.Select(async i =>
			{
				var key = (i % NumberOfLocks).ToString();

				var mylock = await _ProbabilisticMemoryLocker.AcquireLockAsync(null, null, null, key, TimeSpan.FromSeconds(5), null, default).ConfigureAwait(false);
				Operation();
				_ProbabilisticMemoryLocker.ReleaseLock(null, null, null, key, mylock, null);
			}).ToList().AsParallel();

		_ExperimentalMemoryLockerTasks = _shuffledIntegerList
			.Select(async i =>
			{
				var key = (i % NumberOfLocks).ToString();

				var mylock = await _ExperimentalMemoryLocker.AcquireLockAsync(null, null, null, key, TimeSpan.FromSeconds(5), null, default).ConfigureAwait(false);
				Operation();
				_ExperimentalMemoryLocker.ReleaseLock(null, null, null, key, mylock, null);
			}).ToList().AsParallel();

		_AsyncKeyedMemoryLockerTasks = _shuffledIntegerList
			.Select(async i =>
			{
				var key = (i % NumberOfLocks).ToString();

				var mylock = await _AsyncKeyedMemoryLocker.AcquireLockAsync(null, null, null, key, TimeSpan.FromSeconds(5), null, default).ConfigureAwait(false);
				Operation();
				_AsyncKeyedMemoryLocker.ReleaseLock(null, null, null, key, mylock, null);
			}).ToList().AsParallel();

		_AsyncKeyedMemoryLocker2Tasks = _shuffledIntegerList
			.Select(async i =>
			{
				var key = (i % NumberOfLocks).ToString();

				var mylock = await _AsyncKeyedMemoryLocker2.AcquireLockAsync(null, null, null, key, TimeSpan.FromSeconds(5), null, default).ConfigureAwait(false);
				Operation();
				_AsyncKeyedMemoryLocker2.ReleaseLock(null, null, null, key, mylock, null);
			}).ToList().AsParallel();

		_StripedAsyncKeyedMemoryLockerTasks = _shuffledIntegerList
			.Select(async i =>
			{
				var key = (i % NumberOfLocks).ToString();

				var mylock = await _StripedAsyncKeyedMemoryLocker.AcquireLockAsync(null, null, null, key, TimeSpan.FromSeconds(5), null, default).ConfigureAwait(false);
				Operation();
				_StripedAsyncKeyedMemoryLocker.ReleaseLock(null, null, null, key, mylock, null);
			}).ToList().AsParallel();
	}

	private async Task RunTests(ParallelQuery<Task> tasks)
	{
		if (NumberOfLocks == Contention)
		{
			throw new Exception("Thrown on purpose");
		}
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_StandardMemoryLocker.Dispose();
		_ProbabilisticMemoryLocker.Dispose();
		_ExperimentalMemoryLocker.Dispose();
		_AsyncKeyedMemoryLocker.Dispose();
		_AsyncKeyedMemoryLocker2.Dispose();
		_StripedAsyncKeyedMemoryLocker.Dispose();
	}

	[IterationCleanup]
	public void CleanupStandardMemoryLocker()
	{
		_StandardMemoryLockerTasks = null!;
		_ProbabilisticMemoryLockerTasks = null!;
		_ExperimentalMemoryLockerTasks = null!;
		_AsyncKeyedMemoryLockerTasks = null!;
		_AsyncKeyedMemoryLocker2Tasks = null!;
		_StripedAsyncKeyedMemoryLockerTasks = null!;
	}

	private static void Shuffle<T>(IList<T> list)
	{
		int n = list.Count;
		while (n > 1)
		{
			n--;
			int k = Random.Shared.Next(n + 1);
			T value = list[k];
			list[k] = list[n];
			list[n] = value;
		}
	}

	private void Operation()
	{
		for (int i = 0; i < GuidReversals; i++)
		{
			Guid guid = Guid.NewGuid();
			var guidString = guid.ToString();
			guidString = guidString.Reverse().ToString();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
			if (guidString.Length != 53)
			{
				throw new Exception($"Not 53 but {guidString?.Length}");
			}
#pragma warning restore CS8602 // Dereference of a possibly null reference.
		}
	}

	//[Benchmark(Baseline = true)]
	public async Task TestLockStandard()
	{
		await RunTests(_StandardMemoryLockerTasks).ConfigureAwait(false);
	}

	//[Benchmark]
	public async Task TestLockProbabilistic()
	{
		await RunTests(_ProbabilisticMemoryLockerTasks).ConfigureAwait(false);
	}


	//[Benchmark]
	public async Task TestLockExperimental()
	{
		await RunTests(_ExperimentalMemoryLockerTasks).ConfigureAwait(false);
	}

	[Benchmark(Baseline = true)]
	public async Task TestLockAsyncKeyedLock()
	{
		await RunTests(_AsyncKeyedMemoryLockerTasks).ConfigureAwait(false);
	}

	[Benchmark]
	public async Task TestLockAsyncKeyedLock2()
	{
		await RunTests(_AsyncKeyedMemoryLocker2Tasks).ConfigureAwait(false);
	}

	//[Benchmark]
	public async Task TestLockStripedAsyncKeyedLock()
	{
		await RunTests(_StripedAsyncKeyedMemoryLockerTasks).ConfigureAwait(false);
	}
}
