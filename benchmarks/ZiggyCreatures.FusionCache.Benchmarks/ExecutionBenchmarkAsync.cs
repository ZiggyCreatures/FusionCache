using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using ZiggyCreatures.Caching.Fusion.Internals;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class ExecutionBenchmarkAsync
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

	private async Task ExecutorAsync()
	{
		for (int i = 0; i < 1_000_000_000; i++)
		{
			i++;
		}
	}

	[Benchmark(Baseline = true)]
	public async Task WithTimeout()
	{
		await RunUtils.RunAsyncActionAdvancedAsync(
			async _ => await ExecutorAsync(),
			TimeSpan.FromSeconds(2),
			false,
			true
		);
	}

	[Benchmark]
	public async Task WithTimeout2()
	{
		await RunUtils.RunAsyncActionAdvancedAsync(
			async _ => await ExecutorAsync(),
			TimeSpan.FromSeconds(2),
			true,
			true
		);
	}

	[Benchmark]
	public async Task WithoutTimeout()
	{
		await RunUtils.RunAsyncActionAdvancedAsync(
			async _ => await ExecutorAsync(),
			Timeout.InfiniteTimeSpan,
			true,
			true
		);
	}

	[Benchmark]
	public async Task Raw()
	{
		await ExecutorAsync();
	}
}
