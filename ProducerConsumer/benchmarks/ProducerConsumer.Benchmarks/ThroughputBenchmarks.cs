using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;

namespace ProducerConsumer.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ThroughputBenchmarks
{
    private const int ItemCount = 1_000_000;
    private const int ThreadCount = 100;

    [Benchmark(Baseline = true)]
    public void BlockingCollection_1Producer1Consumer()
    {
        using var bc = new BlockingCollection<int>(64);
        var consumer = Task.Run(() =>
        {
            for (var i = 0; i < ItemCount; i++)
                bc.Take();
        });
        for (var i = 0; i < ItemCount; i++)
            bc.Add(i);
        bc.CompleteAdding();
        consumer.Wait();
    }

    [Benchmark]
    public void ProducerConsumerQueue_1Producer1Consumer()
    {
        var queue = new ProducerConsumerQueue<int>();
        var consumer = Task.Run(() =>
        {
            for (var i = 0; i < ItemCount; i++)
                queue.Take();
        });
        for (var i = 0; i < ItemCount; i++)
            queue.Add(i);
        queue.CompleteAdding();
        consumer.Wait();
    }

    [Benchmark]
    public void BlockingCollection_1ProducersNConsumers()
    {
        using var bc = new BlockingCollection<int>(100);
        const int perProducer = ItemCount / ThreadCount;
        var consumers = Enumerable.Range(0, ThreadCount)
            .Select(_ => Task.Run(() => { for (var i = 0; i < perProducer; i++) bc.Take(); }))
            .ToArray();
        for (var i = 0; i < ItemCount; i++)
            bc.Add(i);
        //var producers = Enumerable.Range(0, ThreadCount)
        //    .Select(p => Task.Run(() => { for (var i = 0; i < perProducer; i++) bc.Add(p * perProducer + i); }))
        //    .ToArray();
        //Task.WaitAll(producers);
        bc.CompleteAdding();
        Task.WaitAll(consumers);
    }

    [Benchmark]
    public void ProducerConsumerQueue_1ProducersNConsumers()
    {
        var queue = new ProducerConsumerQueue<int>(100);
        const int perProducer = ItemCount / ThreadCount;
        var consumers = Enumerable.Range(0, ThreadCount)
            .Select(_ => Task.Run(() => { for (var i = 0; i < perProducer; i++) queue.Take(); }))
            .ToArray();
        for (var i = 0; i < ItemCount; i++)
            queue.Add(i);
        //var producers = Enumerable.Range(0, ThreadCount)
        //    .Select(p => Task.Run(() => { for (var i = 0; i < perProducer; i++) queue.Add(p * perProducer + i); }))
        //    .ToArray();
        //Task.WaitAll(producers);
        queue.CompleteAdding();
        Task.WaitAll(consumers);
    }
}
