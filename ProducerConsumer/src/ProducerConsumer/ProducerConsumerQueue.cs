using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProducerConsumer;

/// <summary>
/// Thread-safe producer-consumer queue implementing IProducerConsumerCollection&lt;T&gt; with blocking Add/Take.
/// Uses ConcurrentQueue + SemaphoreSlim for high throughput and low lock contention.
/// 线程安全生产者-消费者队列，实现 IProducerConsumerCollection&lt;T&gt;，支持阻塞式 Add/Take。
/// </summary>
public sealed class ProducerConsumerQueue<T> : IProducerConsumerCollection<T>, IDisposable
{
    private readonly ConcurrentQueue<T> _queue;
    private readonly int _boundedCapacity;
    private readonly SemaphoreSlim _itemsAvailable;
    private readonly SemaphoreSlim? _spaceAvailable;
    private volatile bool _addingCompleted;
    private const int Unbounded = int.MaxValue;

    public ProducerConsumerQueue(int boundedCapacity = Unbounded)
    {
        if (boundedCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Capacity must be at least 1.");

        _boundedCapacity = boundedCapacity;
        _queue = new ConcurrentQueue<T>();
        _itemsAvailable = new SemaphoreSlim(0, boundedCapacity == Unbounded ? int.MaxValue : boundedCapacity);
        _spaceAvailable = boundedCapacity == Unbounded ? null : new SemaphoreSlim(boundedCapacity, boundedCapacity);
    }

    public int Count => _queue.Count;

    public bool IsAddingCompleted => _addingCompleted;

    public bool IsCompleted => _addingCompleted && _queue.IsEmpty;

    public bool IsSynchronized => false;

    public object SyncRoot => throw new NotSupportedException("SyncRoot is not supported for this concurrent collection.");

    public void Add(T item)
    {
        if (_addingCompleted)
            throw new InvalidOperationException("Adding was already completed.");

        if (_spaceAvailable != null)
            _spaceAvailable.Wait();

        try
        {
            if (_addingCompleted)
                throw new InvalidOperationException("Adding was completed while waiting.");

            _queue.Enqueue(item);
            _itemsAvailable.Release();
        }
        catch
        {
            _spaceAvailable?.Release();
            throw;
        }
    }

    public T Take()
    {
        while (true)
        {
            _itemsAvailable.Wait();

            if (_queue.TryDequeue(out var item))
            {
                _spaceAvailable?.Release();
                return item;
            }

            if (_addingCompleted)
                throw new InvalidOperationException("The collection is empty and adding has been completed.");
        }
    }

    public void CompleteAdding()
    {
        if (_addingCompleted)
            return;

        _addingCompleted = true;
        var currentCount = _queue.Count;
        var maxPermits = _spaceAvailable != null ? _boundedCapacity : 10_000_000;
        var releaseCount = Math.Max(0, Math.Min(maxPermits - currentCount, 10_000_000));
        if (releaseCount > 0)
            _itemsAvailable.Release(releaseCount);
    }

    public bool TryAdd(T item)
    {
        if (_addingCompleted)
            return false;

        if (_spaceAvailable != null && !_spaceAvailable.Wait(0))
            return false;

        if (_addingCompleted)
        {
            _spaceAvailable?.Release();
            return false;
        }

        _queue.Enqueue(item);
        _itemsAvailable.Release();
        return true;
    }

    public bool TryTake([MaybeNullWhen(false)] out T item)
    {
        if (!_itemsAvailable.Wait(0))
        {
            item = default;
            return false;
        }

        if (_queue.TryDequeue(out item!))
        {
            _spaceAvailable?.Release();
            return true;
        }

        if (_addingCompleted)
        {
            item = default;
            return false;
        }

        _itemsAvailable.Release();
        item = default;
        return false;
    }

    public void CopyTo(T[] array, int index) => _queue.CopyTo(array, index);

    public void CopyTo(Array array, int index) => ((ICollection)_queue).CopyTo(array, index);

    public T[] ToArray() => _queue.ToArray();

    public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() => _spaceAvailable?.Dispose();
}
