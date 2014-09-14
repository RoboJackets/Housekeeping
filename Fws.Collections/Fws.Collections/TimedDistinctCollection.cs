using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.Caching;

namespace Fws.Collections
{
    /// <summary>
    /// Filters an IEnumerable as LINQ's Distinct() extension method does, but
    /// maintains only a time-limited memory of previous items. This makes it
    /// suitable for long-running (server) operations where LINQ's Distinct()
    /// would gradually consume more and more memory.
    /// </summary>
    /// <typeparam name="T">The type of item being enumerated.</typeparam>
    public class TimedDistinctCollection<T> : IEnumerable<T>
    {
        readonly IEnumerable<T> _source;
        readonly CacheItemPolicy _cachePolicy;
        readonly Func<T, string> _keyMaker;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="source">The IEnumerable whose distinct items we want.</param>
        /// <param name="recallTime">How long this class will remember previous items.</param>
        /// <param name="keyMaker">Makes a cache key out of an item.</param>
        public TimedDistinctCollection(IEnumerable<T> source, TimeSpan recallTime, Func<T, string> keyMaker)
        {
            Contract.Requires(source != null);
            Contract.Requires(keyMaker != null);

            _source = source;
            _keyMaker = keyMaker;
            _cachePolicy = new CacheItemPolicy() { SlidingExpiration = recallTime };
        }

        /// <summary>
        /// Yield the distinct items in the wrapped IEnumerable.
        /// </summary>
        /// <returns>An IEnumerator that may be used in a foreach loop.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            var cache = new MemoryCache(this.GetType().ToString());
            {
                foreach (var item in _source)
                {
                    string key = _keyMaker(item);
                    if (!cache.Contains(key))
                    {
                        cache.Add(key, item, _cachePolicy);
                        yield return item;
                    }
                }
            }
        }

        /// <summary>
        /// Required method for IEnumerable.
        /// </summary>
        /// <returns>The generic enumerator, but as a non-generic version.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
