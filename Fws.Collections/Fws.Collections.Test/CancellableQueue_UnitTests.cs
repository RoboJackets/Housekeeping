using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fws.Collections.Test
{
    [TestClass]
    public class CancellableQueue_UnitTests
    {
        #region Enqueue Tests
        [TestMethod]
        public void Enqueue_IfItemEnqueued_ThenTransformedVersionIsAvailableForDequeue()
        {
            DoTest((q, cts) =>
            {
                q.Enqueue(10);
                string item;
                q.TryDequeue(out item, cts.Token);
                Assert.AreEqual<string>("10", item);
            });
        }
        #endregion

        #region TryDequeue Tests
        [TestMethod]
        public void TryDequeue_IfItemImmediatelyAvailable_ThenProvidesItAndReturnsTrue()
        {
            DoTest((q, cts) =>
            {
                q.Enqueue(7);
                string item;
                Assert.IsTrue(q.TryDequeue(out item, cts.Token));
                Assert.AreEqual("7", item);
            });
        }

        [TestMethod]
        public void TryDequeue_IfMustWaitForItem_ThenProvidesItAndReturnsTrue()
        {
            DoTest((q, cts) =>
            {
                using (var willProvideItemAfterSleep = new SemaphoreSlim(0))
                {
                    var tasks = new Task[]
                        {
                            new Task(() =>
                                {
                                    string item;
                                    willProvideItemAfterSleep.Wait();
                                    Assert.IsTrue(q.TryDequeue(out item, cts.Token));
                                    Assert.AreEqual("7", item);
                                }),
                            new Task(() =>
                                {
                                    willProvideItemAfterSleep.Release();
                                    Thread.Sleep(500);
                                    q.Enqueue(7);
                                })
                        };
                    Parallel.ForEach<Task>(tasks, T => T.Start());
                    Task.WaitAll(tasks);
                }
            });
        }

        [TestMethod]
        public void TryDequeue_IfCancellationAlreadyRequested_ThenReturnsFalse()
        {
            DoTest((q, cts) =>
            {
                cts.Cancel();
                string item;
                Assert.IsFalse(q.TryDequeue(out item, cts.Token));
            });
        }

        [TestMethod]
        public void TryDequeue_IfCancellationRequestedDuringWait_ThenReturnsFalse()
        {
            DoTest((q, cts) =>
            {
                using (var willCancelBeforeEnqueue = new SemaphoreSlim(0))
                {
                    var tasks = new Task[]
                        {
                            new Task(() =>
                                {
                                    string item;
                                    willCancelBeforeEnqueue.Wait();
                                    Assert.IsFalse(q.TryDequeue(out item, cts.Token));
                                }),
                            new Task(() =>
                                {
                                    willCancelBeforeEnqueue.Release();
                                    cts.Cancel();
                                    q.Enqueue(7);
                                })
                        };
                    Parallel.ForEach<Task>(tasks, T => T.Start());
                    Task.WaitAll(tasks);
                }
            });
        }
        #endregion

        #region Utility Methods
        static void DoTest(Action<CancellableQueue<int, string>, CancellationTokenSource> action)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                using (var q = new CancellableQueue<int, string>(i => i.ToString()))
                {
                    action(q, cts);
                }
            }
        }
        #endregion
    }
}
