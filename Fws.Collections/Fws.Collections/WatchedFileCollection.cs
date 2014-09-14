using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fws.Collections
{
    /// <summary>
    /// Detects events related to files in a directory and makes them available to a client class
    /// as an IEnumerable of fully pathed file names. Unlike the .NET FileSystemWatcher, this 
    /// class yields files that exist when the object is constructed. Also, it is not an IDisposable.
    /// </summary>
    /// <remarks>
    /// This class is thread-safe: more than one thread may enumerate the files presented by a 
    /// single instance of this class, and each thread will get all the files.
    /// </remarks>
    abstract public class WatchedFileCollection : IEnumerable<string>
    {
        #region Fields
        readonly string _directory;
        readonly string _filePattern;
        readonly CancellationToken _cancellationToken;
        #endregion

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
        protected WatchedFileCollection(CancellationToken cancellationToken, string directory, string filePattern = null)
        {
            Contract.Requires(directory != null);
            Contract.Requires(cancellationToken != null);

            if (!Directory.Exists(directory))
                throw new ArgumentException(String.Format(Properties.Resources.DirectoryDoesNotExist, directory));

            _directory = directory;
            _filePattern = filePattern ?? "*";
            _cancellationToken = cancellationToken;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Get an enumerator that will yield files until the CancellationToken is canceled.
        /// </summary>
        /// <returns>Fully pathed file names.</returns>
        /// <remarks>
        /// It is possible for a file name to be returned from more than once.
        /// </remarks>
        public IEnumerator<string> GetEnumerator()
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                using (var watcher = new FileSystemWatcher(_directory, _filePattern))
                {
                    using (var queue = new CancellableFileSystemEventQueue())
                    {
                        ConnectWatcherToQueue(watcher, queue);

                        watcher.EnableRaisingEvents = true;
                        // Note that if a file arrives during the following loop, it may be placed on the queue
                        // twice: once when the Create event is raised, and once by the loop itself.
                        foreach (var file in Directory.GetFiles(_directory, _filePattern, SearchOption.TopDirectoryOnly))
                        {
                            queue.Enqueue(this, new FileSystemEventArgs(WatcherChangeTypes.Created, _directory, Path.GetFileName(file)));
                        }

                        if (!_cancellationToken.IsCancellationRequested)
                        {
                            string fileName;
                            while (queue.TryDequeue(out fileName, _cancellationToken))
                                yield return fileName;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Implement this method to connect the FileSystemWatcher's event of interest to the queue.
        /// </summary>
        /// <param name="watcher">The FileSystemWatcher that has been constructed in this class.</param>
        /// <param name="queue">The queue whose Enqueue method will become the event-handler for the FileSystemWatcher.</param>
        internal abstract void ConnectWatcherToQueue(FileSystemWatcher watcher, CancellableFileSystemEventQueue queue);

        /// <summary>
        /// Required method for IEnumerable.
        /// </summary>
        /// <returns>The generic enumerator, but as a non-generic version.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

    }
}
