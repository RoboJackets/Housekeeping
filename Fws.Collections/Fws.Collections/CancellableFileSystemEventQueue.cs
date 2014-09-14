using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fws.Collections
{
    /// <summary>
    /// Queues FileSystemWatcher events, and dequeues the file names
    /// subject to a CancellationToken
    /// </summary>
    internal class CancellableFileSystemEventQueue : CancellableQueue<FileSystemEventArgs, string>, IDisposable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public CancellableFileSystemEventQueue()
            : base(arg => arg.FullPath)
        { }
    }
}