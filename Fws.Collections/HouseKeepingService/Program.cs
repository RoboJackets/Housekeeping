using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using Fws.Collections;
using System.Threading;
using System.IO;

namespace HouseKeepingService
{
    public partial class Program : ServiceBase
    {
        static void Main(string[] args)
        {
            Program service = new Program();

            if (Environment.UserInteractive)
            {
                service.OnStart(args);
                Console.WriteLine("Press any key to stop program");
                Console.Read();
                service.OnStop();
            }
            else
            {
                ServiceBase.Run(service);
            }

        }

        protected override void OnStart(string[] args)
        {
            string pathUser = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string pathDownload = Path.Combine(pathUser, "Downloads");
            string pathDesktop = Path.Combine(pathUser, "Desktop");

            Thread thread1 = new Thread(new ParameterizedThreadStart(Program.MaintainEmptyDirectory));
            thread1.Start(pathDownload);

            Thread thread2 = new Thread(new ParameterizedThreadStart(Program.MaintainEmptyDirectory));
            thread2.Start(pathDesktop);
            //MaintainEmptyDirectory(pathDownload);
            //MaintainEmptyDirectory(pathDesktop);
        }

        public static void MaintainEmptyDirectory(object directory)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            string sourceDir = (String)directory;
            string filePattern = "*.*";
            // Allow files to live for 1 day. 
            var quiesceTime = TimeSpan.FromDays(1);
            var dupeTime = TimeSpan.FromDays(1);

            /*
             * Useful for Debugging
            var quiesceTime = TimeSpan.FromMinutes(1);
            var dupeTime = TimeSpan.FromMinutes(1);
             * */

            var files =
                new ReadyFileCollection(
                    new TimedDistinctCollection<string>(
                        new CreatedFileCollection(cts.Token, sourceDir, filePattern),
                        dupeTime,
                        fileName => fileName),
                    cts.Token,
                    quiesceTime);

            foreach (var file in files)
            {
                Console.WriteLine(file);
                String[] temp = Directory.GetDirectories((String)directory);
                foreach (String dir in temp)
                {
                    Console.WriteLine(dir);
                    Directory.Delete(dir, true);
                }
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            } 
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down
            //necessary to stop your service.
        }
    }
}
