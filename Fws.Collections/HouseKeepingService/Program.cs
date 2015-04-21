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
using System.Reflection;

namespace HouseKeepingService
{
    public partial class Program : ServiceBase
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                //Install service
                if (args[0].Trim().ToLower() == "/i")
                { System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { "/i", Assembly.GetExecutingAssembly().Location }); }

                //Uninstall service                 
                else if (args[0].Trim().ToLower() == "/u")
                { System.Configuration.Install.ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location }); }
            }
            else
            {
                Program service = new Program();
                service.ServiceName = "HouseKeeping";

                //Only applicable when debugging as console application.
                if (Environment.UserInteractive)
                {
                    service.OnStart(args);
                    Console.WriteLine("Press any key to stop program");
                    Console.Read();
                    service.OnStop();
                }

                //The "normal" case.
                else
                {
                    ServiceBase.Run(service);
                }
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
        }

        public static void MaintainEmptyDirectory(object directory)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            string sourceDir = (String)directory;
            //Accept any file
            string filePattern = "*.*";

            //Allow files to live for 1 day. 
            var quiesceTime = TimeSpan.FromDays(1);
            var dupeTime = TimeSpan.FromDays(1);

            //Useful for Debugging
            //var quiesceTime = TimeSpan.FromMinutes(1);
            //var dupeTime = TimeSpan.FromMinutes(1);


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
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    String[] temp = Directory.GetDirectories((String)directory);
                    foreach (String dir in temp)
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch (System.IO.IOException)
                {

                }
            }
        }

        protected override void OnStop()
        {
        }
    }
}
