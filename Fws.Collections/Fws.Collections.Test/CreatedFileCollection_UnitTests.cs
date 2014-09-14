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
    public class CreatedFileCollection_UnitTests
    {
        #region Fields
        string _directory; // Set in TestInitialize
        List<string> _initialFiles; // Fully pathed names of files we might put in the directory to start.
        List<string> _addedFiles; // Fully pathed names of files we might add to the directory.
        List<string> _receivedFiles;
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
            _receivedFiles = new List<string>();
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

        #region GetEnumerable Tests
        [TestMethod]
        public void GetEnumerator_IfFilesArriveDuringEnumeration_ThenYieldsThem()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(5 * 1000))
            {
                int ix = 0;
                CreateFile(_addedFiles[ix++]);
                foreach (var file in new CreatedFileCollection(cts.Token, _directory))
                {
                    _receivedFiles.Add(file);
                    if (ix < _addedFiles.Count)
                    {
                        CreateFile(_addedFiles[ix++]);
                    }
                }
            }

            Assert.IsTrue(_receivedFiles.SequenceEqual(_addedFiles));
        }

        [TestMethod]
        public void GetEnumerator_IfFilesExistedBeforeEnumeration_ThenYieldsThemBeforeNewOnes()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(5 * 1000))
            {
                CreateFiles(_initialFiles);
                bool additionalFilesCreated = false;
                foreach (var file in new CreatedFileCollection(cts.Token, _directory))
                {
                    _receivedFiles.Add(file);
                    if (!additionalFilesCreated)
                    {
                        CreateFiles(_addedFiles);
                        additionalFilesCreated = true;
                    }
                }
            }
            Assert.IsTrue(_receivedFiles.SequenceEqual(_initialFiles.Concat(_addedFiles)));
        }

        [TestMethod]
        public void GetEnumerator_IfFilesPourInDuringEnumeration_ThenNoneAreMissed()
        {
            int numFiles = 1000;
            var someFilesReady = new AutoResetEvent(false);
            var fileQueueTaskStarted = new AutoResetEvent(false);
            var tasks = new Task[]
            {
                // Task to create many files in parallel.
                new Task(() =>
                    {
                        // We want to be poised to wait for files.
                        fileQueueTaskStarted.WaitOne();

                        // Create all the files in several parallel tasks.
                        Parallel.For(0, numFiles, new ParallelOptions(){ MaxDegreeOfParallelism=10}, fileNum =>
                            {
                                if (fileNum >= 20)
                                    someFilesReady.Set();
                                File.Create(Path.Combine(_directory, fileNum.ToString())).Dispose();
                            });
                    }),

                // Task to watch for files.
                new Task(() =>
                    {
                        using (CancellationTokenSource cts = new CancellationTokenSource(5*1000))
                        {
                            fileQueueTaskStarted.Set();

                            // Do not start watching until some files have been created.
                            someFilesReady.WaitOne();
                            foreach (var file in new CreatedFileCollection(cts.Token, _directory))
                            {
                                _receivedFiles.Add(file);
                            }
                        }
                    })
            };
            Parallel.ForEach<Task>(tasks, T => T.Start());
            Task.WaitAll(tasks);

            Assert.AreEqual(numFiles, _receivedFiles.Distinct().Count());
        }

        [TestMethod]
        public void GetEnumerator_IfFilePatternNotUniversal_ThenIgnoresFilesNotMatchingPattern()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(5 * 1000))
            {
                CreateFiles(_initialFiles);
                bool additionalFilesCreated = false;
                foreach (var file in new CreatedFileCollection(cts.Token, _directory, "*.TXT"))
                {
                    _receivedFiles.Add(file);
                    if (!additionalFilesCreated)
                    {
                        CreateFiles(_addedFiles);
                        additionalFilesCreated = true;
                    }
                }
            }
            var expected =
                _initialFiles
                .Concat(_addedFiles)
                .Where(f => Path.GetExtension(f) == ".txt");
            Assert.IsTrue(_receivedFiles.SequenceEqual(expected));
        }

        [TestMethod]
        public void GetEnumerator_IfMultipleThreadsPullingFiles_ThenEachFileReturnedOnce()
        {
            int numFiles = 1000;
            var receivedFiles = new ConcurrentBag<string>();

            // Create all the files.
            for (var fileNum = 0; fileNum < numFiles; ++fileNum)
            {
                File.Create(Path.Combine(_directory, fileNum.ToString())).Dispose();
            };

            using (CancellationTokenSource cts = new CancellationTokenSource(20 * 1000/*a fail-safe*/))
            {
                DateTime startTime = DateTime.UtcNow;
                // Get the results on several parallel threads.
                Parallel.ForEach<string>(new CreatedFileCollection(cts.Token, _directory), new ParallelOptions() { MaxDegreeOfParallelism = 10 }, file =>
                {
                    receivedFiles.Add(file);
                    // If we're getting near the end, assume we can finish in the time we've taken so far.
                    if (receivedFiles.Count == numFiles * 3 / 4)
                        cts.CancelAfter(DateTime.UtcNow - startTime);
                });
            }

            Assert.AreEqual(numFiles, receivedFiles.Count);
            var distinctFiles = receivedFiles.Distinct().Count();
            Assert.AreEqual(numFiles, distinctFiles);
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
