using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ZiggyCreatures.Caching.Fusion.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(Config))]
public class LoggingBenchmark
{
	private class Config : ManualConfig
	{
		public Config()
		{
			AddColumn(StatisticColumn.P95);
			AddDiagnoser(MemoryDiagnoser.Default);
			//AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByMethod);
			AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
			//WithOrderer(new DefaultOrderer(summaryOrderPolicy: SummaryOrderPolicy.FastestToSlowest));
			WithSummaryStyle(BenchmarkDotNet.Reports.SummaryStyle.Default.WithMaxParameterColumnWidth(50));
		}
	}

	private ILogger? _noLogger;
	private ILogger? _nullLogger;
	private Action<string, object[]>? _noLoggerLambda;
	private int Param1 = 21;
	private int Param2 = 42;

	[GlobalSetup]
	public void Setup()
	{
		_noLogger = null;
		_nullLogger = NullLogger.Instance;
		_noLoggerLambda = null;
	}

	[Benchmark(Baseline = true)]
	public void WithNoLogger()
	{
		if (_noLogger?.IsEnabled(LogLevel.Information) ?? false)
		{
			_noLogger.LogInformation("HELLO HI CIAO HALLO {Param1} {Param2}", Param1, Param2);
		}
	}

	[Benchmark]
	public void WithNullLogger()
	{
		if (_nullLogger?.IsEnabled(LogLevel.Information) ?? false)
		{
			_nullLogger.LogInformation("HELLO HI CIAO HALLO {Param1} {Param2}", Param1, Param2);
		}
	}

	[Benchmark]
	public void WithNoLoggerLambda()
	{
		_noLoggerLambda?.Invoke("HELLO HI CIAO HALLO {Param1} {Param2}", new object[] { Param1, Param2 });
	}
}
