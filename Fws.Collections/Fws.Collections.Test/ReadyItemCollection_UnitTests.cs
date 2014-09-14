using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Fws.Collections.Test
{
    [TestClass]
    public class ReadyItemCollection_UnitTests
    {
        [TestMethod]
        public void GetEnumerator_IfNoItemsInput_ThenYieldsNothing()
        {
            using (var cts = new CancellationTokenSource())
            {
                var inputItems = new string[] { };
                var sw = new Stopwatch();
                sw.Start();
                foreach (var item in new ReadyItemCollection<string>(inputItems, cts.Token, item => true, item => DateTime.UtcNow))
                {
                    Assert.Fail(String.Format("Got a mysterious item: {0}.", item ?? "(null)"));
                }
                sw.Stop();
                Assert.IsTrue(sw.ElapsedMilliseconds < 1000, String.Format("That took too long ({0} milliseconds)!", sw.ElapsedMilliseconds));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfAvailableItemsExistAtStart_ThenTheyAreAllYieldedQuickly()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
                var sw = new Stopwatch();
                sw.Start();
                var results = new List<string>(new ReadyItemCollection<string>(input, cts.Token, item => true, item => DateTime.UtcNow.AddMinutes(1)));
                sw.Stop();
                Assert.IsTrue(results.OrderBy(f => f).SequenceEqual(input));
                Assert.IsTrue(sw.ElapsedMilliseconds < 1000, String.Format("That took too long ({0} milliseconds)!", sw.ElapsedMilliseconds));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfNonReadyItemsExistAtStart_ThenYieldsThemQuicklyWhenReady()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
                TimeSpan delay = TimeSpan.FromSeconds(3);
                DateTime timeAvailable = DateTime.UtcNow.Add(delay);
                var sw = new Stopwatch();
                sw.Start();
                var results = new List<string>(new ReadyItemCollection<string>(input, cts.Token, item => DateTime.UtcNow >= timeAvailable, item => DateTime.UtcNow.AddMilliseconds(200)));
                sw.Stop();
                Assert.IsTrue(results.OrderBy(f => f).SequenceEqual(input));
                var minimumMilliseconds = delay.Milliseconds - 200;   // Subtract a margin of error because timings are not always exact.
                Assert.IsTrue(sw.Elapsed.Milliseconds >= minimumMilliseconds, String.Format("Didn't wait for the items to become ready. The items were obtained in {0} milliseconds.", sw.ElapsedMilliseconds));
                Assert.IsTrue(sw.Elapsed.Milliseconds <= delay.Milliseconds + 1000, String.Format("That took too long ({0} milliseconds)!", sw.ElapsedMilliseconds));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfItemsToRemovePresentAtStart_ThenTheyAreNeverYielded()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "bad one", "b" };
                var results = new List<string>(new ReadyItemCollection<string>(input, cts.Token, item => { if (item == "bad one") return null; else return true; }, item => DateTime.UtcNow.AddMilliseconds(10)));
                Assert.IsTrue(results.OrderBy(i => i).SequenceEqual(new string[] { "a", "b" }));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfReadyItemsArriveDuringEnumeration_ThenTheyAreYieldedImmediately()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
                var delayForEachItem = 200;
                var sw = new Stopwatch();
                sw.Start();
                var results = new List<string>(
                    new ReadyItemCollection<string>(
                        input.Select(item => { Task.Delay(delayForEachItem).Wait(); return item; }),
                        cts.Token,
                        item => true,   // Immediately available.
                        item => DateTime.UtcNow.AddSeconds(5)));    // Long delay if we must ask again.
                sw.Stop();
                Assert.IsTrue(results.OrderBy(f => f).SequenceEqual(input));
                Assert.IsTrue(sw.Elapsed.Milliseconds < input.Length * delayForEachItem + 1000, String.Format("That took too long ({0} milliseconds)!", sw.ElapsedMilliseconds));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfNonReadyItemsArriveDuringEnumeration_ThenYieldsThemWhenReady()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
                var delayBeforeFeedingNextItem = 200;
                var delayUntilAllItemsAreReady = TimeSpan.FromSeconds(3);
                DateTime timeItemsAreReady = DateTime.UtcNow.AddSeconds(3);
                var sw = new Stopwatch();
                sw.Start();
                var results = new List<string>(
                    new ReadyItemCollection<string>(
                        input.Select(item => { Task.Delay(delayBeforeFeedingNextItem).Wait(); return item; }),
                        cts.Token,
                        item => DateTime.UtcNow >= timeItemsAreReady,   // All items are ready at this time.
                        item => DateTime.UtcNow.AddMilliseconds(50)));  // Fairly rapid recheck.
                sw.Stop();
                Assert.IsTrue(results.OrderBy(f => f).SequenceEqual(input));
                var minimumMilliseconds = delayUntilAllItemsAreReady.Milliseconds - 200;   // Subtract a margin of error because timings are not always exact.
                Assert.IsTrue(sw.Elapsed.Milliseconds >= minimumMilliseconds, String.Format("Didn't wait for the items to become ready. The items were obtained in {0} milliseconds.", sw.ElapsedMilliseconds));
                Assert.IsTrue(sw.Elapsed.Milliseconds < delayUntilAllItemsAreReady.Milliseconds + 1000, String.Format("That took too long ({0} milliseconds)!", sw.ElapsedMilliseconds));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfItemsToRemoveArriveDuringEnumeration_ThenTheyAreNeverYielded()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "bad 1", "b", "bad 2", "c" };
                var results = new List<string>(new ReadyItemCollection<string>(
                    input.Select(item => { Task.Delay(100).Wait(); return item; }),
                    cts.Token,
                    item => { if (item.Contains("bad")) return null; else return true; },
                    item => DateTime.UtcNow.AddMilliseconds(10)));
                Assert.IsTrue(results.OrderBy(i => i).SequenceEqual(new string[] { "a", "b", "c" }));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfCanceledFromTheStart_ThenYieldsNothing()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var input = new string[] { "a" };
                var results = new List<string>(new ReadyItemCollection<string>(input, cts.Token, item => true, item => DateTime.UtcNow));
                Assert.AreEqual(0, results.Count);
            }
        }

        [TestMethod]
        public void GetEnumerator_IfCanceledDuringEnumeration_ThenYieldsCease()
        {
            using (var cts = new CancellationTokenSource())
            {
                var delayForEachItem = 200;
                cts.CancelAfter(delayForEachItem * 5);
                var input = new string[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
                var results = new List<string>(
                    new ReadyItemCollection<string>(
                        input.Select(item => { Task.Delay(delayForEachItem).Wait(); return item; }),
                        cts.Token,
                        item => true,
                        item => DateTime.UtcNow));
                Assert.IsTrue(results.Count > 0);
                Assert.IsTrue(results.Count < input.Length);
            }
        }

        [TestMethod]
        public void GetEnumerator_IfInputContainsNonReadyDuplicates_ThenYieldsEachJustOnce()
        {
            using (var cts = new CancellationTokenSource())
            {
                var input = new string[] { "a", "b", "c", "a", "b", "c", "a", "b", "c", "x" };
                DateTime timeWhenReady = DateTime.UtcNow.AddSeconds(2);
                var sw = new Stopwatch();
                sw.Start();
                var results = new List<string>(
                    new ReadyItemCollection<string>(
                        input,
                        cts.Token,
                        item => DateTime.UtcNow >= timeWhenReady,   // Allow plenty of time for all input to arrive
                        item => DateTime.UtcNow.AddMilliseconds(200)));
                sw.Stop();

                Assert.IsTrue(results.OrderBy(f => f).SequenceEqual(new[] { "a", "b", "c", "x" }));
            }
        }
    }
}
