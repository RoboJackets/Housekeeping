using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fws.Collections
{
    /// <summary>
    /// Applies a gate to each item in an IEnumerable, presenting it
    /// in an output IEnumerable only when it has been determined to be
    /// "ready." Exactly what that means is up to the caller. In one
    /// implementation, an item might be a file name and "ready"
    /// might mean that the file has quiesced for a certain period of
    /// time and it can be opened.
    /// </summary>
    /// <typeparam name="T">The type of item that is being made available.
    /// </typeparam>
    public class ReadyItemCollection<T> : IEnumerable<T>
    {
        #region Fields
        readonly IEnumerable<T> _inputItems;
        readonly CancellationToken _cancellationToken;
        readonly Func<T, DateTime> _getNextCheckTime;
        readonly Func<T, bool?> _isReady;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="inputItems">An input IEnumerable of items. These items will
        /// come back out again with GetEnumerator, perhaps in a different order,
        /// but only after they have passed the <paramref name="isReady"/> test.</param>
        /// <param name="cancellationToken">If not null, then this class will stop waiting
        /// for items to be ready when the token enters the canceled state.</param>
        /// <param name="isReady">Returns true if the input argument is ready, false if not,
        /// or null if it should be permanently removed from consideration.</param>
        /// <param name="getNextCheckTime">Given an item that is not ready, returns the
        /// next time we should check for availability.</param>
        public ReadyItemCollection(
            IEnumerable<T> inputItems,
            CancellationToken cancellationToken,
            Func<T, bool?> isReady,
            Func<T, DateTime> getNextCheckTime)
        {
            Contract.Requires(inputItems != null);
            Contract.Requires(isReady != null);
            Contract.Requires(getNextCheckTime != null);

            _inputItems = inputItems;
            _cancellationToken = cancellationToken;
            _isReady = isReady;
            _getNextCheckTime = getNextCheckTime;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Get an enumerator that will yield items until the CancellationToken passed to
        /// the constructor is canceled.
        /// </summary>
        /// <returns>The items passed to the constructor, as they become ready.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            // In this dictionary, the Key is the item and
            // the Value is the next time the item should be checked for availability.
            var waitingItems = new ConcurrentDictionary<T, DateTime>();

            var itemAdded = new AutoResetEvent(false);
            Task getItems = null;

            // This task dumps items into the dictionary.
            getItems = Task.Run(() =>
            {
                foreach (var item in _inputItems)
                {
                    if (_cancellationToken.IsCancellationRequested)
                        break;
                    DateTime now = DateTime.UtcNow;
                    waitingItems.AddOrUpdate(item, now, (f, t) => now);
                    itemAdded.Set();
                }
            },
                _cancellationToken);

            // Meanwhile, we dispense items as they are ready.
            while (!_cancellationToken.IsCancellationRequested)
            {
                // This initial value is a failsafe. If we end up waiting this long, 
                // there should be no item ready.
                int millisecondsUntilNextCheck = 60 * 1000;

                // This Task will complete when an item is obtained from the input IEnumerable.
                Task waitForItem = new Task(() => itemAdded.WaitOne());

                if (waitingItems.Count > 0)
                {
                    var firstItemScheduledToCheck = waitingItems
                        .ToArray()
                        .OrderBy(kv => kv.Value)
                        .First()
                        .Key;

                    // A value of true means it is ready; false means it is not; 
                    // null means we should permanently remove it from consideration.
                    bool? firstItemIsReady = _isReady(firstItemScheduledToCheck);

                    if (!firstItemIsReady.HasValue)
                    {
                        DateTime valueNotNeeded;
                        if (!waitingItems.TryRemove(firstItemScheduledToCheck, out valueNotNeeded))
                            throw new InvalidOperationException(Properties.Resources.ReadyItemNotRemoved);
                    }
                    else
                    {
                        if (firstItemIsReady.Value == true)
                        {
                            DateTime valueNotNeeded;
                            if (!waitingItems.TryRemove(firstItemScheduledToCheck, out valueNotNeeded))
                                throw new InvalidOperationException(Properties.Resources.ReadyItemNotRemoved);
                            if (firstItemIsReady.HasValue)
                                yield return firstItemScheduledToCheck;
                        }
                        else
                        {
                            DateTime nextCheckTime = _getNextCheckTime(firstItemScheduledToCheck);
                            waitingItems.AddOrUpdate(firstItemScheduledToCheck, nextCheckTime, (f, t) => nextCheckTime);
                        }
                    }
                }

                if (waitingItems.Count > 0)
                {
                    millisecondsUntilNextCheck = (int)(waitingItems.OrderBy(kv => kv.Value).First().Value - DateTime.UtcNow).TotalMilliseconds;
                    if (millisecondsUntilNextCheck < 0)
                        millisecondsUntilNextCheck = 0;
                }
                else if (getItems.IsCompleted)
                {
                    getItems.Dispose();
                    break;
                }
                waitForItem.Start();
                // Loop around for the next item when any of the following conditions are met:
                // 1) The Task that gets input items has completed.
                // 2) A new item has arrived.
                // 3) We have reached the time where the first item in the queue for checking might be ready.
                // 4) The cancellation token has been canceled.
                try
                {
                    Task.WaitAny(new Task[] { getItems, waitForItem }, millisecondsUntilNextCheck, _cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // This is OK.
                }

                // Just to be tidy, we force the waitForItem Task to complete, and then Dispose it.
                itemAdded.Set();
                Task.WaitAll(waitForItem);
                waitForItem.Dispose();
                itemAdded.Reset();
            }

            if (getItems.IsCompleted)
                getItems.Dispose();
        }

        /// <summary>
        /// Required method for IEnumerable.
        /// </summary>
        /// <returns>The generic enumerator, but as a non-generic version.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}
