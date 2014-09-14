using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fws.Collections.Test
{
    /// <summary>
    /// Tests the TimedDistinctCollection class.
    /// </summary>
    [TestClass]
    public class TimedDistinctCollection_UnitTests
    {
        #region Nested Class for Cache Items
        class MyItem
        {
            public int MyProperty { get; set; }
            public MyItem(int value)
            {
                MyProperty = value;
            }
        }
        #endregion

        [TestMethod]
        public void GetEnumerator_IfDuplicateArrivesWithinTimespan_ThenItIsNotIncludedInEnumeration()
        {
            var strings = new[] { "a", "b", "c", "a", "x" };
            var result = new List<string>(new TimedDistinctCollection<string>(strings, TimeSpan.FromMilliseconds(1000), str => str));
            Assert.IsTrue(result.SequenceEqual(new[] { "a", "b", "c", "x" }));
        }

        [TestMethod]
        public void GetEnumerator_IfDuplicateArrivesAfterTimespan_ThenItIsIncludedInEnumeration()
        {
            var strings = new[] { "a", "b", "c", "a", "x" };
            var timeout = TimeSpan.FromMilliseconds(10);
            var result = new List<string>();
            foreach (var s in new TimedDistinctCollection<string>(strings, timeout, str => str))
            {
                result.Add(s);
                Thread.Sleep(timeout.Milliseconds * 3);
            }
            Assert.IsTrue(result.SequenceEqual(strings));
        }

        [TestMethod]
        public void GetEnumerator_IfKeyMakerSupplied_ThenCacheKeysAreFormedWithIt()
        {
            // If the cache keys ARE NOT being formed properly (for example, if ToString() is being
            // called on each item and ToString() has not been overloaded to provide anything other
            // than the class name), then every item will appear to be in the cache.
            // If the cache keys ARE properly used, then some items will not be in the cache.
            var items = new[] 
            {
                new MyItem(1),
                new MyItem(2),
                new MyItem(3),
                new MyItem(1),  // These three will appear again.
                new MyItem(2),
                new MyItem(3),
                new MyItem(3),  // This one will not appear.
            };
            var timeout = TimeSpan.FromMilliseconds(100);
            var result = new List<MyItem>();
            foreach (var item in new TimedDistinctCollection<MyItem>(items, timeout, i => i.MyProperty.ToString()))
            {
                result.Add(item);
                Thread.Sleep(timeout.Milliseconds / 2);
            }
            Assert.AreEqual(6, result.Count);
        }
    }
}
