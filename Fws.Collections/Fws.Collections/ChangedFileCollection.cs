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
    /// Detects changes to files in a directory and makes them available to a client class
    /// as an IEnumerable of fully pathed file names. Unlike the .NET FileSystemWatcher, this 
    /// class yields files that exist when the object is constructed. Also, it is not an IDisposable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When this class is instantiated, any matching files already in the directory are reported
    /// as changed.
    /// </para>
    /// <para>
    /// This class is thread-safe: more than one thread may enumerate the files presented by a 
    /// single instance of this class, and each thread will get all the files.
    /// </para>
    /// </remarks>
    public sealed class ChangedFileCollection : WatchedFileCollection, IEnumerable<string>
    {
        #region Constructor
        /// <summary>
        /// Constructor. 
        /// </summary>
        /// <param name="cancellationToken">This class will terminate the enumeration of
        /// files when and only when the token enters the canceled state.</param>
        /// <param name="directory">The directory to watch.</param>
        /// <param name="filePattern">A pattern to match in the file name. Example: "*.txt".
        /// Null means all files.</param>
        /// <remarks>Duplicates may be returned on the queue. See remarks for the class.</remarks>
        public ChangedFileCollection(CancellationToken cancellationToken, string directory, string filePattern = null)
            : base(cancellationToken, directory, filePattern)
        {
        }
        #endregion

        #region Methods
        /// <summary>
        /// Connect the FileSystemWatcher's event of interest to the queue.
        /// </summary>
        /// <param name="watcher">The FileSystemWatcher that has been constructed in this class.</param>
        /// <param name="queue">The queue whose Enqueue method will become the event-handler for the FileSystemWatcher.</param>
        internal override void ConnectWatcherToQueue(FileSystemWatcher watcher, CancellableFileSystemEventQueue queue)
        {
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Changed += queue.Enqueue;
        }
        #endregion
    }
}
