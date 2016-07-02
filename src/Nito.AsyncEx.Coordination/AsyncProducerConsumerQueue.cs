using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
    /// <summary>
    /// An async-compatible producer/consumer queue.
    /// </summary>
    /// <typeparam name="T">The type of elements contained in the queue.</typeparam>
    [DebuggerDisplay("Count = {_queue.Count}, MaxCount = {_maxCount}")]
    [DebuggerTypeProxy(typeof(AsyncProducerConsumerQueue<>.DebugView))]
    public sealed class AsyncProducerConsumerQueue<T>
    {
        /// <summary>
        /// The underlying queue.
        /// </summary>
        private readonly Queue<T> _queue;

        /// <summary>
        /// The maximum number of elements allowed in the queue.
        /// </summary>
        private readonly int _maxCount;

        /// <summary>
        /// The mutual-exclusion lock protecting <c>_queue</c> and <c>_completed</c>.
        /// </summary>
        private readonly AsyncLock _mutex;

        /// <summary>
        /// A condition variable that is signalled when the queue is not full.
        /// </summary>
        private readonly AsyncConditionVariable _completedOrNotFull;

        /// <summary>
        /// A condition variable that is signalled when the queue is completed or not empty.
        /// </summary>
        private readonly AsyncConditionVariable _completedOrNotEmpty;

        /// <summary>
        /// Whether this producer/consumer queue has been marked complete for adding.
        /// </summary>
        private bool _completed;

        /// <summary>
        /// Creates a new async-compatible producer/consumer queue with the specified initial elements and a maximum element count.
        /// </summary>
        /// <param name="collection">The initial elements to place in the queue.</param>
        /// <param name="maxCount">The maximum element count. This must be greater than zero.</param>
        public AsyncProducerConsumerQueue(IEnumerable<T> collection, int maxCount)
        {
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "The maximum count must be greater than zero.");
            _queue = collection == null ? new Queue<T>() : new Queue<T>(collection);
            if (maxCount < _queue.Count)
                throw new ArgumentException("The maximum count cannot be less than the number of elements in the collection.", "maxCount");
            _maxCount = maxCount;

            _mutex = new AsyncLock();
            _completedOrNotFull = new AsyncConditionVariable(_mutex);
            _completedOrNotEmpty = new AsyncConditionVariable(_mutex);
        }

        /// <summary>
        /// Creates a new async-compatible producer/consumer queue with the specified initial elements.
        /// </summary>
        /// <param name="collection">The initial elements to place in the queue.</param>
        public AsyncProducerConsumerQueue(IEnumerable<T> collection)
            : this(collection, int.MaxValue)
        {
        }

        /// <summary>
        /// Creates a new async-compatible producer/consumer queue with a maximum element count.
        /// </summary>
        /// <param name="maxCount">The maximum element count. This must be greater than zero.</param>
        public AsyncProducerConsumerQueue(int maxCount)
            : this(null, maxCount)
        {
        }

        /// <summary>
        /// Creates a new async-compatible producer/consumer queue.
        /// </summary>
        public AsyncProducerConsumerQueue()
            : this(null, int.MaxValue)
        {
        }

        /// <summary>
        /// Whether the queue is empty. This property assumes that the <c>_mutex</c> is already held.
        /// </summary>
        private bool Empty { get { return _queue.Count == 0; } }

        /// <summary>
        /// Whether the queue is full. This property assumes that the <c>_mutex</c> is already held.
        /// </summary>
        private bool Full { get { return _queue.Count == _maxCount; } }

        /// <summary>
        /// Synchronously marks the producer/consumer queue as complete for adding.
        /// </summary>
        public void CompleteAdding()
        {
            using (_mutex.Lock())
            {
                _completed = true;
                _completedOrNotEmpty.NotifyAll();
                _completedOrNotFull.NotifyAll();
            }
        }

        /// <summary>
        /// Attempts to enqueue an item.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the enqueue operation.</param>
        public async Task<bool> AttemptEnqueueAsync(T item, CancellationToken cancellationToken)
        {
            using (await _mutex.LockAsync().ConfigureAwait(false))
            {
                // Wait for the queue to be not full.
                while (Full && !_completed)
                    await _completedOrNotFull.WaitAsync(cancellationToken).ConfigureAwait(false);

                // If the queue has been marked complete, then abort.
                if (_completed)
                    return false;

                _queue.Enqueue(item);
                _completedOrNotEmpty.Notify();
                return true;
            }
        }

        /// <summary>
        /// Attempts to enqueue an item. This method may block the calling thread.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the enqueue operation.</param>
        public bool AttemptEnqueue(T item, CancellationToken cancellationToken)
        {
            using (_mutex.Lock())
            {
                // Wait for the queue to be not full.
                while (Full && !_completed)
                    _completedOrNotFull.Wait(cancellationToken);

                // If the queue has been marked complete, then abort.
                if (_completed)
                    return false;

                _queue.Enqueue(item);
                _completedOrNotEmpty.Notify();
                return true;
            }
        }

        /// <summary>
        /// Attempts to enqueue an item to the producer/consumer queue. Returns <c>false</c> if the producer/consumer queue has completed adding.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public Task<bool> AttemptEnqueueAsync(T item)
        {
            return AttemptEnqueueAsync(item, CancellationToken.None);
        }

        /// <summary>
        /// Attempts to enqueue an item to the producer/consumer queue. Returns <c>false</c> if the producer/consumer queue has completed adding. This method may block the calling thread.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public bool AttemptEnqueue(T item)
        {
            return AttemptEnqueue(item, CancellationToken.None);
        }

        /// <summary>
        /// Enqueues an item to the producer/consumer queue. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the enqueue operation.</param>
        public async Task EnqueueAsync(T item, CancellationToken cancellationToken)
        {
            var result = await AttemptEnqueueAsync(item, cancellationToken).ConfigureAwait(false);
            if (!result)
                throw new InvalidOperationException("Enqueue failed; the producer/consumer queue has completed adding.");
        }

        /// <summary>
        /// Enqueues an item to the producer/consumer queue. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding. This method may block the calling thread.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the enqueue operation.</param>
        public void Enqueue(T item, CancellationToken cancellationToken)
        {
            var result = AttemptEnqueue(item, cancellationToken);
            if (!result)
                throw new InvalidOperationException("Enqueue failed; the producer/consumer queue has completed adding.");
        }

        /// <summary>
        /// Enqueues an item to the producer/consumer queue. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public Task EnqueueAsync(T item)
        {
            return EnqueueAsync(item, CancellationToken.None);
        }

        /// <summary>
        /// Enqueues an item to the producer/consumer queue. This method may block the calling thread. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        public void Enqueue(T item)
        {
            Enqueue(item, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously waits until an item is available to dequeue. Returns <c>false</c> if the producer/consumer queue has completed adding and there are no more items.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the asynchronous wait.</param>
        public async Task<bool> OutputAvailableAsync(CancellationToken cancellationToken)
        {
            using (await _mutex.LockAsync().ConfigureAwait(false))
            {
                while (Empty && !_completed)
                    await _completedOrNotEmpty.WaitAsync(cancellationToken).ConfigureAwait(false);
                return !Empty;
            }
        }

        /// <summary>
        /// Asynchronously waits until an item is available to dequeue. Returns <c>false</c> if the producer/consumer queue has completed adding and there are no more items.
        /// </summary>
        public Task<bool> OutputAvailableAsync()
        {
            return OutputAvailableAsync(CancellationToken.None);
        }

        /// <summary>
        /// Provides a (synchronous) consuming enumerable for items in the producer/consumer queue.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the synchronous enumeration.</param>
        public IEnumerable<T> GetConsumingEnumerable(CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = AttemptDequeue(cancellationToken);
                if (!result.IsSuccess)
                    yield break;
                yield return result.Result;
            }
        }

        /// <summary>
        /// Provides a (synchronous) consuming enumerable for items in the producer/consumer queue.
        /// </summary>
        public IEnumerable<T> GetConsumingEnumerable()
        {
            return GetConsumingEnumerable(CancellationToken.None);
        }

        /// <summary>
        /// Attempts to dequeue an item.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the dequeue operation.</param>
        public async Task<TryResult<T>> AttemptDequeueAsync(CancellationToken cancellationToken)
        {
            using (await _mutex.LockAsync().ConfigureAwait(false))
            {
                while (Empty && !_completed)
                    await _completedOrNotEmpty.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (_completed && Empty)
                    return new TryResult<T>();
                var item = _queue.Dequeue();
                _completedOrNotFull.Notify();
                return new TryResult<T>(true, item);
            }
        }

        /// <summary>
        /// Attempts to dequeue an item. This method may block the calling thread.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the dequeue operation.</param>
        public TryResult<T> AttemptDequeue(CancellationToken cancellationToken)
        {
            using (_mutex.Lock())
            {
                while (Empty && !_completed)
                    _completedOrNotEmpty.Wait(cancellationToken);
                if (_completed && Empty)
                    return new TryResult<T>();
                var item = _queue.Dequeue();
                _completedOrNotFull.Notify();
                return new TryResult<T>(true, item);
            }
        }

        /// <summary>
        /// Attempts to dequeue an item from the producer/consumer queue.
        /// </summary>
        public Task<TryResult<T>> AttemptDequeueAsync()
        {
            return AttemptDequeueAsync(CancellationToken.None);
        }

        /// <summary>
        /// Attempts to dequeue an item from the producer/consumer queue. This method may block the calling thread.
        /// </summary>
        public TryResult<T> AttemptDequeue()
        {
            return AttemptDequeue(CancellationToken.None);
        }

        /// <summary>
        /// Dequeues an item from the producer/consumer queue. Returns the dequeued item. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding and is empty.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the dequeue operation.</param>
        /// <returns>The dequeued item.</returns>
        public async Task<T> DequeueAsync(CancellationToken cancellationToken)
        {
            var ret = await AttemptDequeueAsync(cancellationToken).ConfigureAwait(false);
            if (!ret.IsSuccess)
                throw new InvalidOperationException("Dequeue failed; the producer/consumer queue has completed adding and is empty.");
            return ret.Result;
        }

        /// <summary>
        /// Dequeues an item from the producer/consumer queue. Returns the dequeued item. This method may block the calling thread. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding and is empty.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to abort the dequeue operation.</param>
        public T Dequeue(CancellationToken cancellationToken)
        {
            var ret = AttemptDequeue(cancellationToken);
            if (!ret.IsSuccess)
                throw new InvalidOperationException("Dequeue failed; the producer/consumer queue has completed adding and is empty.");
            return ret.Result;
        }

        /// <summary>
        /// Dequeues an item from the producer/consumer queue. Returns the dequeued item. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding and is empty.
        /// </summary>
        /// <returns>The dequeued item.</returns>
        public Task<T> DequeueAsync()
        {
            return DequeueAsync(CancellationToken.None);
        }

        /// <summary>
        /// Dequeues an item from the producer/consumer queue. Returns the dequeued item. This method may block the calling thread. Throws <see cref="InvalidOperationException"/> if the producer/consumer queue has completed adding and is empty.
        /// </summary>
        /// <returns>The dequeued item.</returns>
        public T Dequeue()
        {
            return Dequeue(CancellationToken.None);
        }

        [DebuggerNonUserCode]
        internal sealed class DebugView
        {
            private readonly AsyncProducerConsumerQueue<T> _queue;

            public DebugView(AsyncProducerConsumerQueue<T> queue)
            {
                _queue = queue;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T[] Items
            {
                get { return _queue._queue.ToArray(); }
            }
        }
    }
}
