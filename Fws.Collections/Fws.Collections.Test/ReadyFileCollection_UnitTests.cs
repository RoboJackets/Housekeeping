using System;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace Fws.Collections.Test
{
    [TestClass]
    public class ReadyFileCollection_UnitTests
    {
        #region FileIsReady Tests
        [TestMethod]
        public void FileIsReady_IfFileDoesNotExist_ThenReturnsNull()
        {
            Assert.IsFalse(ReadyFileCollection.FileIsReady(Guid.NewGuid().ToString(), TimeSpan.FromSeconds(5), FileAccess.Read, FileShare.None).HasValue);
        }

        [TestMethod]
        public void FileIsReady_IfFileWrittenTooRecently_ThenReturnsFalse()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                Assert.IsFalse(ReadyFileCollection.FileIsReady(tempFile, TimeSpan.FromSeconds(5), FileAccess.Read, FileShare.None).Value);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void FileIsReady_IfFileCannotBeOpened_ThenReturnsFalse()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                using (var strm = File.OpenWrite(tempFile))
                {
                    Assert.IsFalse(ReadyFileCollection.FileIsReady(tempFile, TimeSpan.FromSeconds(0), FileAccess.Read, FileShare.Read).Value);
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void FileIsReady_IfQuiesceTimeIsDefaultAndFileCanBeOpened_ThenReturnsTrue()
        {
            var tempFile = Path.GetTempFileName();
            Thread.Sleep(100);
            try
            {
                Assert.IsTrue(ReadyFileCollection.FileIsReady(tempFile, default(TimeSpan), FileAccess.Write, FileShare.None).Value);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void FileIsReady_IfFileIsQuiescedAndCanBeOpened_ThenReturnsTrue()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var quiesceTime = TimeSpan.FromSeconds(2);
                Thread.Sleep((int)(quiesceTime.TotalMilliseconds * 2));
                Assert.IsTrue(ReadyFileCollection.FileIsReady(tempFile, quiesceTime, FileAccess.Write, FileShare.None).Value);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        #endregion

        #region GetNextCheckTime Tests
        [TestMethod]
        public void GetNextCheckTime_ReturnsNowPlusParameter()
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan delay = TimeSpan.FromSeconds(5);
            DateTime nextCheck = ReadyFileCollection.GetNextCheckTime(delay);
            DateTime expected = now.Add(delay);
            // Check that the actual returned value is tolerably close to the expected one.
            Assert.IsTrue(nextCheck >= expected.AddMilliseconds(-100));
            Assert.IsTrue(nextCheck <= expected.AddMilliseconds(+100));
        }
        #endregion
    }
}
