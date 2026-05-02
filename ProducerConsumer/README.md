# ProducerConsumer

Thread-safe, high-performance producer-consumer queue in C# implementing `IProducerConsumerCollection<T>`, with benchmark comparison against `BlockingCollection<T>`.

## Build & Test

```bash
dotnet build
dotnet test
```

## Run Benchmarks

```bash
dotnet run -c Release --project benchmarks/ProducerConsumer.Benchmarks/ProducerConsumer.Benchmarks.csproj -- --filter "*Throughput*"
```

See [BENCHMARKS.md](BENCHMARKS.md) for methodology and detailed results.

### Multi-Threaded Performance vs BlockingCollection

| Scenario | BlockingCollection | ProducerConsumerQueue | Allocated (BC vs PCQ) |
|----------|-------------------|----------------------|------------------------|
| 1P1C, 100K items | ~25 ms | ~27 ms | 551 KB vs 880 B |
| 4P4C, 100K items | ~63 ms | ~66 ms | 1219 KB vs 2.3 KB |

Throughput: comparable (~95% of BlockingCollection). Memory: ProducerConsumerQueue allocates ~0.2% of BlockingCollection.

## Usage

```csharp
var queue = new ProducerConsumerQueue<int>(boundedCapacity: 64);

// Producers
queue.Add(1);
queue.Add(2);
queue.CompleteAdding();  // signal no more items

// Consumers
var item = queue.Take();  // blocks until available
if (queue.TryTake(out var another)) { /* ... */ }
```

## Projects

- `src/ProducerConsumer` – Class library with `ProducerConsumerQueue<T>`
- `tests/ProducerConsumer.Tests` – xUnit tests
- `benchmarks/ProducerConsumer.Benchmarks` – BenchmarkDotNet benchmarks
