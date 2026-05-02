using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Columns;
using ProducerConsumer.Benchmarks;

var config = DefaultConfig.Instance
    .HideColumns(Column.Error, Column.StdDev);

BenchmarkSwitcher.FromAssembly(typeof(ThroughputBenchmarks).Assembly).Run(args, config);
