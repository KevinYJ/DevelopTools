using System.Collections.Concurrent;

namespace ProducerConsumer.Tests;

public class ProducerConsumerQueueTests
{
    [Fact]
    public void SingleProducerSingleConsumer_Correctness()
    {
        var queue = new ProducerConsumerQueue<int>();
        const int count = 1000;

        var consumed = new List<int>();
        var consumer = Task.Run(() =>
        {
            for (var i = 0; i < count; i++)
                consumed.Add(queue.Take());
        });

        for (var i = 0; i < count; i++)
            queue.Add(i);

        consumer.Wait();
        Assert.Equal(count, consumed.Count);
        Assert.Equal(Enumerable.Range(0, count), consumed);
    }

    [Fact]
    public void MultipleProducersMultipleConsumers_Correctness()
    {
        var queue = new ProducerConsumerQueue<int>(100);
        const int producerCount = 4;
        const int consumerCount = 4;
        const int itemsPerProducer = 250;

        var consumed = new System.Collections.Concurrent.ConcurrentBag<int>();
        var consumers = Enumerable.Range(0, consumerCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < itemsPerProducer; i++)
                    consumed.Add(queue.Take());
            }))
            .ToArray();

        var producers = Enumerable.Range(0, producerCount)
            .Select(p => Task.Run(() =>
            {
                for (var i = 0; i < itemsPerProducer; i++)
                    queue.Add(p * itemsPerProducer + i);
            }))
            .ToArray();

        Task.WaitAll(producers);
        Task.WaitAll(consumers);

        Assert.Equal(producerCount * itemsPerProducer, consumed.Count);
        Assert.Equal(producerCount * itemsPerProducer, consumed.Distinct().Count());
    }

    [Fact]
    public void BoundedCapacity_BlocksAddWhenFull()
    {
        var queue = new ProducerConsumerQueue<int>(2);
        queue.Add(1);
        queue.Add(2);

        var addTask = Task.Run(() => queue.Add(3));
        Assert.False(addTask.Wait(100));

        var taken = queue.Take();
        Assert.True(addTask.Wait(1000));
        Assert.Equal(1, taken);
    }

    [Fact]
    public void BoundedCapacity_TryAddReturnsFalseWhenFull()
    {
        var queue = new ProducerConsumerQueue<int>(2);
        Assert.True(queue.TryAdd(1));
        Assert.True(queue.TryAdd(2));
        Assert.False(queue.TryAdd(3));
    }

    [Fact]
    public void EmptyQueue_BlocksTake()
    {
        var queue = new ProducerConsumerQueue<int>();

        var takeTask = Task.Run(() => queue.Take());
        Assert.False(takeTask.Wait(100));

        queue.Add(42);
        Assert.True(takeTask.Wait(1000));
        Assert.Equal(42, takeTask.Result);
    }

    [Fact]
    public void EmptyQueue_TryTakeReturnsFalse()
    {
        var queue = new ProducerConsumerQueue<int>();
        Assert.False(queue.TryTake(out _));
    }

    [Fact]
    public void CompleteAdding_IsCompletedWhenDrained()
    {
        var queue = new ProducerConsumerQueue<int>();
        queue.Add(1);
        queue.CompleteAdding();

        Assert.False(queue.IsCompleted);
        Assert.Equal(1, queue.Take());
        Assert.True(queue.IsCompleted);
    }

    [Fact]
    public void CompleteAdding_TakeThrowsWhenEmpty()
    {
        var queue = new ProducerConsumerQueue<int>();
        queue.CompleteAdding();

        Assert.Throws<InvalidOperationException>(() => queue.Take());
    }

    [Fact]
    public void CompleteAdding_AddThrows()
    {
        var queue = new ProducerConsumerQueue<int>();
        queue.CompleteAdding();

        Assert.Throws<InvalidOperationException>(() => queue.Add(1));
    }

    [Fact]
    public void CompleteAdding_UnblocksWaitingConsumers()
    {
        var queue = new ProducerConsumerQueue<int>();
        var takeEx = new List<Exception>();
        var consumers = Enumerable.Range(0, 3)
            .Select(_ => Task.Run(() =>
            {
                try
                {
                    queue.Take();
                }
                catch (InvalidOperationException ex)
                {
                    lock (takeEx) takeEx.Add(ex);
                }
            }))
            .ToArray();

        Thread.Sleep(50);
        queue.CompleteAdding();
        Task.WaitAll(consumers);

        Assert.Equal(3, takeEx.Count);
    }

    [Fact]
    public void IProducerConsumerCollection_TryAddTryTake()
    {
        IProducerConsumerCollection<int> queue = new ProducerConsumerQueue<int>();
        Assert.True(queue.TryAdd(1));
        Assert.True(queue.TryTake(out var item));
        Assert.Equal(1, item);
        Assert.False(queue.TryTake(out _));
    }

    [Fact]
    public void IProducerConsumerCollection_CopyToAndToArray()
    {
        var queue = new ProducerConsumerQueue<int>();
        queue.Add(1);
        queue.Add(2);

        var arr = queue.ToArray();
        Assert.Equal([1, 2], arr);

        var copy = new int[2];
        queue.CopyTo(copy, 0);
        Assert.Equal([1, 2], copy);
    }

    [Fact]
    public void Stress_LongRunningConcurrentOperations()
    {
        var queue = new ProducerConsumerQueue<int>(64);
        const int totalItems = 10_000;
        const int producerCount = 4;
        const int consumerCount = 4;

        var consumed = new System.Collections.Concurrent.ConcurrentBag<int>();

        var consumers = Enumerable.Range(0, consumerCount)
            .Select(_ => Task.Run(() =>
            {
                while (true)
                {
                    if (queue.TryTake(out var item))
                        consumed.Add(item);
                    else if (queue.IsCompleted)
                        break;
                    else
                        Thread.Sleep(0);
                }
            }))
            .ToArray();

        var producers = Enumerable.Range(0, producerCount)
            .Select(p => Task.Run(() =>
            {
                for (var i = 0; i < totalItems / producerCount; i++)
                {
                    while (!queue.TryAdd(p * (totalItems / producerCount) + i))
                        Thread.Sleep(0);
                }
            }))
            .ToArray();

        Task.WaitAll(producers);
        queue.CompleteAdding();
        Task.WaitAll(consumers);

        Assert.Equal(totalItems, consumed.Count);
    }

    #region Multi-Threaded Safety Tests / 多线程安全性测试

    [Fact]
    public void ThreadSafety_NoDataLoss_ManyProducersManyConsumers()
    {
        var queue = new ProducerConsumerQueue<int>(128);
        const int totalItems = 10_000;
        const int producerCount = 8;
        const int consumerCount = 8;
        var consumed = new ConcurrentBag<int>();

        var consumers = Enumerable.Range(0, consumerCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < totalItems / consumerCount; i++)
                    consumed.Add(queue.Take());
            }))
            .ToArray();

        var producers = Enumerable.Range(0, producerCount)
            .Select(p => Task.Run(() =>
            {
                for (var i = 0; i < totalItems / producerCount; i++)
                    queue.Add(p * (totalItems / producerCount) + i);
            }))
            .ToArray();

        Task.WaitAll(producers);
        queue.CompleteAdding();
        Task.WaitAll(consumers);

        Assert.Equal(totalItems, consumed.Count);
        Assert.Equal(totalItems, consumed.Distinct().Count());
    }

    [Fact]
    public void ThreadSafety_ConcurrentTryAddTryTake_NoCorruption()
    {
        var queue = new ProducerConsumerQueue<int>(64);
        const int totalItems = 5_000;
        const int producerCount = 4;
        const int consumerCount = 4;
        var consumed = new ConcurrentBag<int>();
        var addFailures = new ConcurrentBag<int>();

        var consumers = Enumerable.Range(0, consumerCount)
            .Select(_ => Task.Run(() =>
            {
                var count = 0;
                while (count < totalItems / consumerCount)
                {
                    if (queue.TryTake(out var item))
                    {
                        consumed.Add(item);
                        count++;
                    }
                    else if (queue.IsCompleted)
                        break;
                }
            }))
            .ToArray();

        var producers = Enumerable.Range(0, producerCount)
            .Select(p => Task.Run(() =>
            {
                for (var i = 0; i < totalItems / producerCount; i++)
                {
                    var value = p * (totalItems / producerCount) + i;
                    while (!queue.TryAdd(value))
                        addFailures.Add(value);
                }
            }))
            .ToArray();

        Task.WaitAll(producers);
        queue.CompleteAdding();
        Task.WaitAll(consumers);

        Assert.Equal(totalItems, consumed.Count);
        Assert.Equal(totalItems, consumed.Distinct().Count());
    }

    [Fact]
    public void ThreadSafety_MixedAddTakeAndTryAddTryTake()
    {
        var queue = new ProducerConsumerQueue<int>(32);
        const int count = 5_000;
        var consumed = new ConcurrentBag<int>();

        var blockingConsumer = Task.Run(() =>
        {
            for (var i = 0; i < count / 2; i++)
                consumed.Add(queue.Take());
        });

        var tryConsumer = Task.Run(() =>
        {
            var got = 0;
            while (got < count / 2)
            {
                if (queue.TryTake(out var item))
                {
                    consumed.Add(item);
                    got++;
                }
                else if (queue.IsCompleted)
                    break;
            }
        });

        var blockingProducer = Task.Run(() =>
        {
            for (var i = 0; i < count / 2; i++)
                queue.Add(i);
        });

        var tryProducer = Task.Run(() =>
        {
            for (var i = count / 2; i < count; i++)
            {
                while (!queue.TryAdd(i))
                    Thread.SpinWait(100);
            }
        });

        Task.WaitAll(blockingProducer, tryProducer);
        queue.CompleteAdding();
        Task.WaitAll(blockingConsumer, tryConsumer);

        Assert.Equal(count, consumed.Count);
        Assert.Equal(count, consumed.Distinct().Count());
    }

    [Fact]
    public void ThreadSafety_ToArraySnapshotConsistency()
    {
        var queue = new ProducerConsumerQueue<int>(100);
        var results = new ConcurrentBag<int[]>();
        const int count = 200;

        var producer = Task.Run(() =>
        {
            for (var i = 0; i < count; i++)
                queue.Add(i);
            queue.CompleteAdding();
        });

        var snapshotter = Task.Run(() =>
        {
            while (!queue.IsCompleted)
            {
                var arr = queue.ToArray();
                if (arr.Length > 0)
                {
                    results.Add(arr);
                    Assert.Equal(arr.Length, arr.Distinct().Count());
                }
                Thread.Yield();
            }
        });

        var consumer = Task.Run(() =>
        {
            try
            {
                while (true)
                    queue.Take();
            }
            catch (InvalidOperationException) { }
        });

        producer.Wait();
        snapshotter.Wait();
        consumer.Wait();

        Assert.All(results, arr => Assert.Equal(arr.Length, arr.Distinct().Count()));
    }

    [Fact]
    public void ThreadSafety_CountNeverNegative()
    {
        var queue = new ProducerConsumerQueue<int>(); // unbounded to avoid Add blocking
        const int iterations = 300;
        var errors = new ConcurrentBag<Exception>();

        var producers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    try
                    {
                        queue.Add(i);
                        var c = queue.Count;
                        if (c < 0) throw new InvalidOperationException($"Count was negative: {c}");
                    }
                    catch (Exception ex) { errors.Add(ex); }
                }
            }))
            .ToArray();

        var consumers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    try
                    {
                        if (queue.TryTake(out _))
                        {
                            var c = queue.Count;
                            if (c < 0) throw new InvalidOperationException($"Count was negative: {c}");
                        }
                    }
                    catch (Exception ex) { errors.Add(ex); }
                }
            }))
            .ToArray();

        Task.WaitAll(producers.Concat(consumers).ToArray());
        Assert.Empty(errors);
    }

    [Fact]
    public void ThreadSafety_CompleteAddingRacesWithProducers()
    {
        for (var trial = 0; trial < 10; trial++)
        {
            var queue = new ProducerConsumerQueue<int>(64);
            const int itemsToAdd = 500;
            var consumed = new ConcurrentBag<int>();
            var addExceptions = new ConcurrentBag<Exception>();

            var producer = Task.Run(() =>
            {
                for (var i = 0; i < itemsToAdd; i++)
                {
                    try { queue.Add(i); }
                    catch (InvalidOperationException ex) { addExceptions.Add(ex); }
                }
            });

            var completer = Task.Run(() =>
            {
                Thread.Sleep(1);
                queue.CompleteAdding();
            });

            var consumer = Task.Run(() =>
            {
                try
                {
                    while (true)
                        consumed.Add(queue.Take());
                }
                catch (InvalidOperationException) { }
            });

            Task.WaitAll(producer, completer, consumer);
            Assert.True(consumed.Count > 0);
            Assert.Equal(consumed.Count, consumed.Distinct().Count());
        }
    }

    [Fact]
    public void ThreadSafety_ConcurrentGetEnumerator()
    {
        var queue = new ProducerConsumerQueue<int>(50);
        for (var i = 0; i < 50; i++)
            queue.Add(i);

        var enumerations = new ConcurrentBag<List<int>>();
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() =>
            {
                var list = queue.ToArray().ToList();
                enumerations.Add(list);
            }))
            .ToArray();

        Task.WaitAll(tasks);
        Assert.All(enumerations, list =>
        {
            Assert.InRange(list.Count, 0, 50);
            Assert.Equal(list, list.OrderBy(x => x).ToList());
        });
    }

    #endregion
}
