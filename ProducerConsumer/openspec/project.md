# Project Context / 项目背景

## Purpose / 目的

Build a thread-safe, high-performance producer-consumer collection in C# for multi-threaded data processing, with benchmark comparison against Microsoft's `BlockingCollection<T>`.  
建立线程安全、高性能的 C# 生产者-消费者集合，用于多线程数据处理，并与微软的 `BlockingCollection<T>` 进行 benchmark 比较。

## Tech Stack / 技术栈

- C# / .NET 8+
- BenchmarkDotNet（性能 benchmark）
- xUnit 或 NUnit（单元测试）

## Project Conventions / 项目惯例

### Code Style / 程序代码风格

- Follow Microsoft C# coding conventions / 遵循微软 C# 编码惯例
- Use `async`/`await` where appropriate; blocking APIs use `Monitor`/`lock` for producer-consumer semantics / 适时使用 `async`/`await`；阻塞式 API 使用 `Monitor`/`lock` 实现生产者-消费者语义

### Architecture Patterns / 架构模式

- Producer-consumer pattern with bounded queue / 有界队列的生产者-消费者模式
- Thread-safe collections with explicit completion signaling / 具明确完成信号的线程安全集合

### Testing Strategy / 测试策略

- Unit tests for correctness (single/multi producer-consumer) / 单元测试验证正确性（单/多生产者-消费者）
- Stress tests for concurrency / 并发压力测试
- BenchmarkDotNet for performance comparison / BenchmarkDotNet 用于性能对比

### Git Workflow / Git 工作流

- Main branch; feature branches for changes / 主分支；功能分支用于变更

## Domain Context / 领域背景

- Concurrency: multi-threaded producers and consumers sharing a queue / 并发：多线程生产者与消费者共享队列
- Backpressure: bounded capacity to prevent unbounded memory growth / 背压：有界容量防止无界内存增长
- Completion: graceful shutdown via `CompleteAdding` / `IsCompleted` / 完成：通过 `CompleteAdding` / `IsCompleted` 优雅关闭

## Important Constraints / 重要约束

- Must remain thread-safe under arbitrary producer/consumer counts / 必须在任意生产者/消费者数量下保持线程安全
- API should align with `BlockingCollection<T>` for fair comparison / API 应与 `BlockingCollection<T>` 对齐以便公平比较

## External Dependencies / 外部依赖

- `System.Collections.Concurrent`（用于参考实现对比）
- BenchmarkDotNet
- xUnit/NUnit

