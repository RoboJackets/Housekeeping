using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fws.Collections.Test
{
    [TestClass]
    public class ChangedFileCollection_UnitTests
    {
        #region Fields
        string _directory; // Set in TestInitialize
        List<string> _initialFiles; // Fully pathed names of files we might put in the directory to start.
        List<string> _addedFiles; // Fully pathed names of files we might add to the directory.
        #endregion

        #region Test Initialization and Cleanup
        [TestInitialize]
        public void TestInitialize()
        {
            _initialFiles = new List<string>();
            _addedFiles = new List<string>();
            _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_directory);
            foreach (var name in new[] { "A.txt", "A1.TIF", "A2.TIF", "B.txt", "C.txt", "D.txt" })
            {
                _initialFiles.Add(Path.Combine(_directory, name));
            }
            foreach (var name in new[] { "T.txt", "T1.TIF", "U2.TIF", "V.txt", "W.txt", "X.txt", "Y.txt", "Z.txt" })
            {
                _addedFiles.Add(Path.Combine(_directory, name));
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            for (int attempt = 0; attempt < 10; ++attempt)
            {
                try
                {
                    Directory.Delete(_directory, true);
                    break;
                }
                catch (IOException ex)
                {
                    // It sometimes takes a while for the O/S to realize that a file is free. Give it a few tries before giving up.
                    if (ex.Message.Contains("being used by another process"))
                        Thread.Sleep(100);
                    else
                        throw;
                }
            }
        }
        #endregion

        #region Tests
        [TestMethod]
        public void GetEnumerator_IfFilesPresentOnConstruction_ThenTheyAreYielded()
        {
            CreateFiles(_initialFiles);
            using (var cts = new CancellationTokenSource(1000))
            {
                var results = new ChangedFileCollection(cts.Token, _directory);
                Assert.IsTrue(results.SequenceEqual(_initialFiles));
            }
        }

        [TestMethod]
        public void GetEnumerator_IfFilesChangeDuringEnumeration_ThenTheyAreYielded()
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                AutoResetEvent collectionCreated = new AutoResetEvent(false);
                var tasks = new Task[]
                {
                    new Task( () =>
                        {
                            var collection = new ChangedFileCollection(cts.Token, _directory);
                            collectionCreated.Set();
                            var results = collection.ToArray();
                            Assert.IsTrue(results.SequenceEqual(_addedFiles));
                        }),
                    new Task( () =>
                        {
                            collectionCreated.WaitOne();
                            Thread.Sleep(500);
                            CreateFiles(_addedFiles);
                        })
                };
                tasks[0].Start();
                tasks[1].Start();
                Task.WaitAll(tasks);
            }
        }

        [TestMethod]
        public void GetEnumerator_IfNoFilesEver_ThenYieldsNothing()
        {
            using (var cts = new CancellationTokenSource(500))
            {
                var results = new ChangedFileCollection(cts.Token, _directory);
                Assert.AreEqual(0, results.Count());
            }
        }
        #endregion

        #region Utility Methods
        static void CreateFiles(IEnumerable<string> files)
        {
            foreach (var f in files)
                CreateFile(f);
        }

        static void CreateFile(string file)
        {
            File.WriteAllLines(file, new[] { "This file is from a unit test and may be deleted." });
        }
        #endregion
    }
}
