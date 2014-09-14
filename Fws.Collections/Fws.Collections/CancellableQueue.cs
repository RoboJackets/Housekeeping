using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

namespace Fws.Collections
{
    /// <summary>
    /// Encapsulates a ConcurrentQueue from which items can be dequeued subject
    /// to a CancellationToken. Additionally, the items can undergo a transformation
    /// when they are enqueued.
    /// </summary>
    /// <typeparam name="TEnqueue">The type of object that is enqueued.</typeparam>
    /// <typeparam name="TDequeue">The type of object that will be dequeued.</typeparam>
    internal class CancellableQueue<TEnqueue, TDequeue> : IDisposable
    {
        // The encapsulated queue.
        readonly ConcurrentQueue<TDequeue> _queue = new ConcurrentQueue<TDequeue>();

        // This semaphore is released (incremented) each time an item is enqueued
        // and waited on during dequeue.
        readonly SemaphoreSlim _itemEnqueued = new SemaphoreSlim(0);

        // A function to transform the item as it arrives to be enqueued, to the
        // form we want to dequeue.
        readonly Func<TEnqueue, TDequeue> _enqueueToDequeue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="enqueueToDequeue">A function to transform the item as it arrives
        /// to be enqueued, to the form we want to dequeue.</param>
        public CancellableQueue(Func<TEnqueue, TDequeue> enqueueToDequeue)
        {
            Contract.Requires(enqueueToDequeue != null);
            _enqueueToDequeue = enqueueToDequeue;
        }

        /// <summary>
        /// Attempt to get an item from the queue. If an item is not immediately
        /// available, this method waits until one is, or until the cancellation token
        /// indicates that cancellation has been requested.
        /// </summary>
        /// <param name="item">The item, if cancellation was not requested.</param>
        /// <returns>True if got an item; false if not.</returns>
        public bool TryDequeue(out TDequeue item, CancellationToken cancellationToken)
        {
            item = default(TDequeue);
            // Avoid the OperationCanceledException if we can.
            if (cancellationToken.IsCancellationRequested)
                return false;
            try
            {
                _itemEnqueued.Wait(cancellationToken);
                return _queue.TryDequeue(out item);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// Enqueue an item.
        /// </summary>
        /// <param name="item">The item to be enqueued. 
        /// It will undergo the transformation from the enqueue-to-dequeue
        /// Func given in the constructor.</param>
        public void Enqueue(TEnqueue item)
        {
            _queue.Enqueue(_enqueueToDequeue(item));
            _itemEnqueued.Release();
        }

        /// <summary>
        /// An enqueueing method whose signature is suitable for event-handling applications.
        /// </summary>
        /// <param name="sender">The object sending the item.</param>
        /// <param name="item">Args from the event handler.</param>
        public void Enqueue(object sender, TEnqueue item)
        {
            Enqueue(item);
        }

        /// <summary>
        /// Dispose this object.
        /// </summary>
        public void Dispose()
        {
            _itemEnqueued.Dispose();
        }
    }
}
