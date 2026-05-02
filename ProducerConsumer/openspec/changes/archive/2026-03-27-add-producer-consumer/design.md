## Context / 背景

In .NET, `BlockingCollection<T>` wraps `IProducerConsumerCollection<T>` and provides blocking operations. Alternative designs (e.g., lock-free queues, segmented queues, or channel-style APIs) may offer different throughput/latency characteristics for specific workloads.  
Stakeholders: developers needing predictable producer-consumer behavior with performance guarantees.

在 .NET 中，`BlockingCollection<T>` 封装 `IProducerConsumerCollection<T>` 并提供阻塞操作。替代设计（如 lock-free 队列、分段队列或 channel 风格 API）可能为特定工作负载提供不同的吞吐量/延迟特性。  
利益相关者：需要可预测且具性能保证的生产者-消费者行为的开发者。

## Goals / Non-Goals / 目标与非目标

**Goals / 目标:**
- Thread-safe producer-consumer collection with blocking Take/Add  
  具阻塞式 Take/Add 的线程安全生产者-消费者集合
- Bounded capacity support  
  支持有界容量
- Completion signaling (no more items)  
  完成信号（无更多项目）
- Benchmark harness comparing against `BlockingCollection<T>`  
  与 `BlockingCollection<T>` 对比的 benchmark 框架

**Non-Goals / 非目标:**
- Replace `BlockingCollection<T>` in general; focus on measurable alternative for comparison  
  并非全面取代 `BlockingCollection<T>`，专注于可量化的对比
- Support for multiple consumers with fair scheduling  
  不支持多消费者公平调度
- Non-blocking TryTake/TryAdd only (blocking variants are in scope)  
  不以仅 TryTake/TryAdd 为限（阻塞变体在范围内）

## Decisions / 决策

### D1: Implementation Strategy (Lock-based vs Lock-free) / 实现策略（基于锁 vs 无锁）

- **Decision:** Start with a lock-based design using `Monitor`/`lock` and a backing queue. Optimize only if benchmarks show clear gains.  
  **决策：** 先采用基于 `Monitor`/`lock` 及后端队列的 lock-based 设计。仅在 benchmark 显示明显收益时再优化。
- **Alternatives considered:**
  - Lock-free (e.g., `ConcurrentQueue<T>` + manual signaling): Higher complexity, may not always outperform on .NET.  
    无锁（如 `ConcurrentQueue<T>` + 手动信号）：复杂度较高，在 .NET 上未必更优。
  - Channel API (`System.Threading.Channels`): Different API style; comparison focus stays on `BlockingCollection<T>`-like semantics.  
    Channel API：API 风格不同；对比聚焦于类 `BlockingCollection<T>` 的语义。
- **Rationale:** Simplicity first; lock-based design is sufficient for many workloads and easier to validate.  
  **理由：** 优先简化；lock-based 设计足以应对多数工作负载且更易验证。

### D2: IProducerConsumerCollection Implementation / 实现 IProducerConsumerCollection

- **Decision:** The class SHALL implement `System.Collections.Concurrent.IProducerConsumerCollection<T>`, providing `TryAdd` and `TryTake` as required by the interface, plus `CopyTo`, `ToArray`, `GetEnumerator`, and `Count`.  
  **决策：** 类 SHALL 实现 `System.Collections.Concurrent.IProducerConsumerCollection<T>`，提供接口要求的 `TryAdd` 与 `TryTake`，以及 `CopyTo`、`ToArray`、`GetEnumerator` 与 `Count`。
- **Rationale:** Enables interoperability with .NET ecosystem; the collection can be used as a backing store for `BlockingCollection<T>` and integrates with APIs expecting `IProducerConsumerCollection<T>`.  
  **理由：** 实现与 .NET 生态互操作；集合可作为 `BlockingCollection<T>` 的后端存储，并与接受 `IProducerConsumerCollection<T>` 的 API 集成。

### D3: API Shape / API 形态

- **Decision:** In addition to the interface, provide `Add(T)` (blocking when full), `Take()` (blocking when empty), and `CompleteAdding()` / `IsCompleted` for shutdown.  
  **决策：** 除接口外，提供 `Add(T)`（满时阻塞）、`Take()`（空时阻塞），以及用于关闭的 `CompleteAdding()` / `IsCompleted`。
- **Rationale:** Align with `BlockingCollection<T>` semantics to enable direct comparison and familiar usage.  
  **理由：** 与 `BlockingCollection<T>` 语义一致，便于直接对比和熟悉用法。

### D4: Bounded Capacity / 有界容量

- **Decision:** Support bounded capacity (configurable, optional unbounded via `int.MaxValue` or similar).  
  **决策：** 支持有界容量（可配置，可选通过 `int.MaxValue` 等表示无界）。
- **Rationale:** Matches `BlockingCollection<T>` and is common in backpressure scenarios.  
  **理由：** 与 `BlockingCollection<T>` 一致，且常见于背压情境。

### D5: Benchmark Metrics / Benchmark 指标

- **Decision:** Measure throughput (items/sec) and latency percentiles (P50, P95, P99) for varying producer/consumer counts and item sizes.  
  **决策：** 针对不同生产者/消费者数量与项目大小，量测吞吐量（items/sec）及延迟百分位（P50、P95、P99）。
- **Rationale:** Enables objective comparison across workloads (e.g., single producer–single consumer vs many-to-many).  
  **理由：** 支持跨工作负载的客观对比（如单生产者-单消费者 vs 多对多）。

## Risks / Trade-offs / 风险与权衡

| Risk / 风险 | Mitigation / 缓解 |
|-------------|-------------------|
| Lock contention under high parallelism / 高并行下的锁竞争 | Start with simple lock; consider segmented locks or lock-free if data shows bottleneck / 先用简单锁；若数据显示瓶颈再考虑分段锁或无锁 |
| Benchmark environment variance / Benchmark 环境差异 | Document hardware, .NET version, warm-up runs / 记录硬件、.NET 版本、预热回合 |
| API divergence from BlockingCollection / 与 BlockingCollection 的 API 差异 | Keep API intentionally similar for fair comparison / 刻意保持 API 相似以利公平对比 |

## Migration Plan / 迁移计划

N/A—new capability, no migration. / 无需迁移（新功能）。

### D6: Backing Structure / 后端结构

- **Decision:** Use `Queue<T>` as the backing structure for best performance and strict FIFO order preservation.  
  **决策：** 采用 `Queue<T>` 作为后端结构，以获得最佳性能并严格保持 FIFO 顺序。
- **Alternatives considered:**
  - `LinkedList<T>`: O(1) add/remove at head/tail, but poorer cache locality and more allocations per node.  
    `LinkedList<T>`：头尾 O(1) 增删，但缓存局部性较差，每节点有额外分配。
  - Array-based circular buffer: Similar to `Queue<T>` internally; `Queue<T>` encapsulates this pattern.  
    基于数组的循环缓冲：与 `Queue<T>` 内部类似；`Queue<T>` 已封装该模式。
- **Rationale:** `Queue<T>` provides O(1) enqueue/dequeue, contiguous memory for cache efficiency, and guaranteed FIFO ordering.  
  **理由：** `Queue<T>` 提供 O(1) 入队/出队、连续内存以提升缓存效率，并保证 FIFO 顺序。

### D7: Target .NET Version / 目标 .NET 版本

- **Decision:** Target .NET 8 and above.  
  **决策：** 目标为 .NET 8 及以上版本。
- **Rationale:** Enables use of latest runtime and library optimizations; no need for broader backward compatibility.  
  **理由：** 可使用最新运行时与库优化；无需支持更早版本。

## Open Questions / 待决问题

_None remaining._ / _无待决问题。_
