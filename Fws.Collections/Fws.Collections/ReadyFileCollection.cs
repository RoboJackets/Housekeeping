using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fws.Collections
{
    /// <summary>
    /// Construct this class on an IEnumerable of file names. Its GetEnumerator method
    /// will then yield the files as they become ready, meaning they have not been 
    /// written to for a given period of time, and are likely to be openable in 
    /// the given mode. 
    /// </summary>
    /// <remarks>
    /// Beware! It is possible that a file could become un-ready after this IEnumerable 
    /// yields it, but before you try to open it. Always check for Exceptions when you
    /// open a file, even one that is supposedly 'ready'.
    /// </remarks>
    public class ReadyFileCollection : ReadyItemCollection<string>, IEnumerable<string>
    {
        static DateTime _lastWriteTimeUtcForFileThatDoesNotExist
            = File.GetLastWriteTimeUtc(Guid.NewGuid().ToString());

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="inputFiles">
        /// An IEnumerable of file names.</param>
        /// <param name="cancellationToken">
        /// The output enumeration will terminate if this token goes into the canceled state.</param>
        /// <param name="quiesceTime">
        /// For a file to be considered ready, it must not have been written to for this long.</param>
        /// <param name="fileAccess">
        /// For a file to be considered ready, it must be openable with this access method.</param>
        /// <param name="fileShare">
        /// For a file to be considered ready, it must be openable with this sharing parameter.</param>
        public ReadyFileCollection(
            IEnumerable<string> inputFiles,
            CancellationToken cancellationToken,
            TimeSpan quiesceTime = default(TimeSpan),
            FileAccess fileAccess = FileAccess.Read,
            FileShare fileShare = FileShare.None)
            : base(
                inputFiles,
                cancellationToken,
                file => FileIsReady(file, quiesceTime, fileAccess, fileShare),
                file => GetNextCheckTime(quiesceTime))
        {
        }

        /// <summary>
        /// Tell whether the file is ready.
        /// </summary>
        /// <param name="fileName">The name of a file.</param>
        /// <returns>True if the file is ready; false if not; 
        /// null if the file is no longer present or access is denied.</returns>
        /// <remarks>It is possible that another process will open the file after you have
        /// determined that it is available with this method, but before you have opened it.
        /// Always catch Exceptions when you try to open a file, even one that is supposedly
        /// 'ready'.</remarks>
        static internal bool? FileIsReady(
            string fileName, TimeSpan quiesceTime, FileAccess fileAccess, FileShare fileShare)
        {
            try
            {
                var lastWriteTime = File.GetLastWriteTimeUtc(fileName);
                if (lastWriteTime.Add(quiesceTime) >= DateTime.UtcNow)
                    return false;

                // Surprisingly, File.GetLastWriteTimeUtc does not throw if the file does not 
                // exist. To handle that situation, we compare to a known value that for
                // the condition.
                if (lastWriteTime == _lastWriteTimeUtcForFileThatDoesNotExist)
                    return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null; // Not allowed to see this one.
            }
            catch (FileNotFoundException)
            {
                return null; // Nothing to see.
            }
            // Other problems are more serious and we want to let the Exception bubble up.

            try
            {
                using (File.Open(fileName, FileMode.Open, fileAccess, fileShare))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tell when we want to check on this file next.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="waitTime"></param>
        /// <returns></returns>
        static internal DateTime GetNextCheckTime(TimeSpan waitTime)
        {
            return DateTime.UtcNow.Add(waitTime);
        }
    }
}
