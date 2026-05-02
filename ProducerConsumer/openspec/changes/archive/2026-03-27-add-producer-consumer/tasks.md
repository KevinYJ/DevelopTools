# Implementation Tasks / 实现任务

## 1. Project Setup / 项目设置

- [x] 1.1 Create solution with class library project for ProducerConsumer / 建立含 ProducerConsumer 类库项目的解决方案
- [x] 1.2 Add BenchmarkDotNet (or equivalent) benchmark project / 新增 BenchmarkDotNet（或等效）benchmark 项目
- [x] 1.3 Add xUnit/NUnit test project / 新增 xUnit/NUnit 测试项目

## 2. Core Implementation / 核心实现

- [x] 2.1 Implement `ProducerConsumerQueue<T>` implementing `IProducerConsumerCollection<T>` with bounded capacity / 实现 `IProducerConsumerCollection<T>` 且具 bounded capacity 的 `ProducerConsumerQueue<T>`
- [x] 2.2 Implement blocking `Add(T)` and `Take()` methods / 实现阻塞式 `Add(T)` 与 `Take()` 方法
- [x] 2.3 Implement `TryAdd` / `TryTake` non-blocking variants / 实现非阻塞 `TryAdd` / `TryTake` 变体
- [x] 2.4 Implement `CompleteAdding()` and `IsCompleted` for shutdown / 实现用于关闭的 `CompleteAdding()` 与 `IsCompleted`
- [x] 2.5 Ensure thread safety under concurrent producers and consumers / 确保多生产者/多消费者并发下的线程安全

## 3. Tests / 测试

- [x] 3.1 Unit tests: single producer–single consumer correctness / 单元测试：单生产者-单消费者正确性
- [x] 3.2 Unit tests: multiple producers–multiple consumers correctness / 单元测试：多生产者-多消费者正确性
- [x] 3.3 Unit tests: bounded capacity blocking behavior / 单元测试：有界容量阻塞行为
- [x] 3.4 Unit tests: `CompleteAdding` and `IsCompleted` behavior / 单元测试：`CompleteAdding` 与 `IsCompleted` 行为
- [x] 3.5 Stress tests: long-running concurrent operations / 压力测试：长时间并发操作

## 4. Benchmark / 性能测试

- [x] 4.1 Define benchmark scenarios (throughput, latency percentiles) / 定义 benchmark 场景（吞吐量、延迟百分位）
- [x] 4.2 Implement benchmarks for `ProducerConsumerQueue<T>` / 实现 `ProducerConsumerQueue<T>` 的 benchmark
- [x] 4.3 Implement benchmarks for `BlockingCollection<T>` (same scenarios) / 实现 `BlockingCollection<T>` 的 benchmark（相同场景）
- [x] 4.4 Add README or doc with benchmark results and methodology / 新增含 benchmark 结果与方法的 README 或文档

## 5. Validation / 验证

- [x] 5.1 Run full test suite / 执行完整测试套件
- [x] 5.2 Run benchmarks and document results / 执行 benchmark 并记录结果
