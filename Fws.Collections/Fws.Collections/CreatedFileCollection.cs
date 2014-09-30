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
    /// Detects the arrival of files in a directory and makes them available to a client class
    /// as an IEnumerable of fully pathed file names. Unlike the .NET FileSystemWatcher, this 
    /// class yields files that exist when the object is constructed. Also, it is not an IDisposable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If a file arrives during the execution of this class's constructor, it may be reported more than
    /// once. Also, some programs write their files in such a way that the underlying FileSystemWatcher
    /// will fire a Create event more than once. In those cases, this class will yield the
    /// file multiple times.
    /// </para><para>
    /// Client code must account for these possibilities. It is envisioned that wrapping classes may
    /// refine the yielded files by waiting for them to quiesce, filtering out duplicates, etc.
    /// </para>
    /// <para>
    /// This class is thread-safe: more than one thread may enumerate the files presented by a 
    /// single instance of this class, and each thread will get all the files.
    /// </para>
    /// </remarks>
    public sealed class CreatedFileCollection : WatchedFileCollection, IEnumerable<string>
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
        public CreatedFileCollection(CancellationToken cancellationToken, string directory, string filePattern = null)
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
            // Restrict the NotifyFilter to all that's necessary for Create events.
            // This minimizes the likelihood that FileSystemWatcher's buffer will be overwhelmed.
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName;

            watcher.Created += queue.Enqueue;
        }
        #endregion
    }
}
