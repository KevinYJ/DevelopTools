## ADDED Requirements

### Requirement: Producer-Consumer Queue Implementation（生产者-消费者队列实现）

The system SHALL provide a thread-safe `ProducerConsumerQueue<T>` class that implements `IProducerConsumerCollection<T>` and supports blocking Add and Take operations for multi-threaded producer-consumer scenarios.  
系统 SHALL 提供线程安全的 `ProducerConsumerQueue<T>` 类，实现 `IProducerConsumerCollection<T>` 接口，支持阻塞式 Add 与 Take，用于多线程生产者-消费者场景。

#### Scenario: IProducerConsumerCollection interface compliance（实现 IProducerConsumerCollection 接口）

- **WHEN** the class is used as an `IProducerConsumerCollection<T>`  
  **当** 类被用作 `IProducerConsumerCollection<T>`
- **THEN** it SHALL provide `TryAdd(T)` and `TryTake(out T)` with semantics matching the interface contract  
  **则** SHALL 提供符合接口契约的 `TryAdd(T)` 与 `TryTake(out T)`
- **AND** it SHALL implement `CopyTo`, `ToArray`, `GetEnumerator`, and `Count` as required by the interface  
  **且** SHALL 实现接口要求的 `CopyTo`、`ToArray`、`GetEnumerator` 与 `Count`

#### Scenario: Blocking Add when queue is full（队列满时的阻塞 Add）

- **WHEN** the queue has reached its bounded capacity and a producer calls `Add(T)` / **当** 队列已达有界容量且生产者调用 `Add(T)`
- **THEN** the call blocks until space becomes available / **则** 调用阻塞直到有空位
- **AND** the item is enqueued successfully when space is available / **且** 有空位时项目成功入队

#### Scenario: Blocking Take when queue is empty（队列空时的阻塞 Take）

- **WHEN** the queue is empty and a consumer calls `Take()` / **当** 队列为空且消费者调用 `Take()`
- **THEN** the call blocks until an item is available / **则** 调用阻塞直到有项目可用
- **AND** the item is dequeued and returned when available / **且** 有项目时出队并返回

#### Scenario: Non-blocking TryAdd when queue is full（队列满时的非阻塞 TryAdd）

- **WHEN** the queue is full and a producer calls `TryAdd(T)` / **当** 队列已满且生产者调用 `TryAdd(T)`
- **THEN** the method returns immediately with a failure indication (e.g., `false`) / **则** 方法立即返回失败表示（如 `false`）
- **AND** the item is not enqueued / **且** 项目未入队

#### Scenario: Non-blocking TryTake when queue is empty（队列空时的非阻塞 TryTake）

- **WHEN** the queue is empty and a consumer calls `TryTake(out T?)` / **当** 队列为空且消费者调用 `TryTake(out T?)`
- **THEN** the method returns immediately with a failure indication (e.g., `false`) / **则** 方法立即返回失败表示（如 `false`）
- **AND** no item is dequeued / **且** 无项目出队

#### Scenario: CompleteAdding signals no more items（CompleteAdding 表示无更多项目）

- **WHEN** a producer calls `CompleteAdding()` / **当** 生产者调用 `CompleteAdding()`
- **THEN** `IsCompleted` SHALL become true once the queue is drained / **则** 队列清空后 `IsCompleted` SHALL 为 true
- **AND** consumers blocked on `Take()` SHALL be unblocked with an appropriate indication (e.g., exception or sentinel) when the queue is empty and adding is complete / **且** 队列为空且新增完成时，在 `Take()` 上阻塞的消费者 SHALL 以适当表示（如异常或哨兵）解除阻塞

#### Scenario: Thread safety under concurrent access（并发访问下的线程安全）

- **WHEN** multiple producer threads call Add/TryAdd and multiple consumer threads call Take/TryTake concurrently / **当** 多个生产者线程并发调用 Add/TryAdd，多个消费者线程并发调用 Take/TryTake
- **THEN** all operations SHALL complete without data corruption or lost items / **则** 所有操作 SHALL 完成且无数据损毁或遗失
- **AND** FIFO ordering SHALL be preserved for enqueue/dequeue operations / **且** 入队/出队操作 SHALL 保持 FIFO 顺序

---

### Requirement: Performance Benchmark Against BlockingCollection（与 BlockingCollection 的性能 Benchmark）

The system SHALL include a benchmark harness that compares `ProducerConsumerQueue<T>` against `System.Collections.Concurrent.BlockingCollection<T>` under equivalent workloads.  
系统 SHALL 包含 benchmark 框架，在相同工作负载下比较 `ProducerConsumerQueue<T>` 与 `System.Collections.Concurrent.BlockingCollection<T>`。

#### Scenario: Throughput comparison（吞吐量比较）

- **WHEN** the benchmark runs with the same producer count, consumer count, and item count for both implementations / **当** benchmark 以相同生产者数、消费者数与项目数对两种实现执行
- **THEN** throughput (items per second) SHALL be measured for each / **则** SHALL 对两者量测吞吐量（每秒项目数）
- **AND** results SHALL be reported in a format suitable for comparison (e.g., BenchmarkDotNet output) / **且** 结果 SHALL 以适于对比的格式回报（如 BenchmarkDotNet 输出）

#### Scenario: Latency percentiles comparison（延迟百分位比较）

- **WHEN** the benchmark runs latency-focused scenarios (e.g., single producer–single consumer) / **当** benchmark 执行以延迟为主的场景（如单生产者-单消费者）
- **THEN** P50, P95, and P99 latency SHALL be measured for both implementations / **则** SHALL 对两种实现量测 P50、P95、P99 延迟
- **AND** results SHALL be reported for comparison / **且** 结果 SHALL 回报以供比较

#### Scenario: Reproducible benchmark configuration（可重现的 benchmark 配置）

- **WHEN** running benchmarks / **当** 执行 benchmark
- **THEN** .NET version, warm-up runs, and iteration count SHALL be documented or configurable / **则** .NET 版本、预热回合与迭代次数 SHALL 被记录或可配置
- **AND** the methodology SHALL allow reproducible runs on the same hardware / **且** 方法 SHALL 允许在相同硬件上可重现执行
