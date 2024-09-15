using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class ExecutionBenchmarkSync
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(
				StatisticColumn.P95
			);
		}
	}

	private void Executor()
	{
		for (int i = 0; i < 1_000_000_000; i++)
		{
			i++;
		}
	}

	[Benchmark(Baseline = true)]
	public void WithTimeout()
	{
		RunUtils.RunSyncActionAdvanced(
			_ => Executor(),
			TimeSpan.FromSeconds(2),
			false,
			true
		);
	}

	[Benchmark]
	public void WithTimeout2()
	{
		RunUtils.RunSyncActionAdvanced(
			_ => Executor(),
			TimeSpan.FromSeconds(2),
			true,
			true
		);
	}

	[Benchmark]
	public void WithoutTimeout()
	{
		RunUtils.RunSyncActionAdvanced(
			_ => Executor(),
			Timeout.InfiniteTimeSpan,
			true,
			true
		);
	}

	[Benchmark]
	public void Raw()
	{
		Executor();
	}
}
