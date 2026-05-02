# Change: Add High-Performance Producer-Consumer Class
# 变更：新增高性能生产者-消费者类

## Why / 为什么

需要一个线程安全、高性能的 C# 生产者-消费者集合类，用于多线程环境下的生产者与消费者协作，并能与微软的 `BlockingCollection<T>` 进行性能比较验证。

A thread-safe, high-performance producer-consumer collection is needed for multi-threaded data processing scenarios, with measurable performance comparison against Microsoft's `BlockingCollection<T>`.

## What Changes / 变更内容

- Add a new `ProducerConsumerQueue<T>` class implementing `IProducerConsumerCollection<T>` with blocking Take/Add semantics  
  新增 `ProducerConsumerQueue<T>` 类，实现 `IProducerConsumerCollection<T>` 接口，支持阻塞式 Take/Add 语义
- Implement thread-safe enqueue/dequeue using lock-free or lock-based design  
  以 lock-free 或 lock-based 设计实现线程安全的入队/出队
- Add configurable bounded capacity support  
  支持可配置的有界容量
- Add completion/signaling for graceful shutdown  
  新增完成/信号机制以支持优雅关闭
- Add benchmark project comparing throughput and latency vs `BlockingCollection<T>`  
  新增 benchmark 项目，比较与 `BlockingCollection<T>` 的吞吐量与延迟
- Add unit tests for correctness and concurrency behavior  
  新增单元测试以验证正确性与并发行为

## Impact / 影响

- Affected specs: `producer-consumer` (new) / 受影响规格：`producer-consumer`（新增）
- Affected code: New class library project, benchmark project, test project  
  受影响程序：新增类库项目、benchmark 项目、测试项目
