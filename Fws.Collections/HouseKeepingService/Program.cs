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
    /* static class Program
     {
         /// <summary>
         /// The main entry point for the application.
         /// </summary>
         static void Main()
         {
             ServiceBase[] ServicesToRun;
             ServicesToRun = new ServiceBase[] 
             { 
                 new Service1() 
             };
             ServiceBase.Run(ServicesToRun);
         }

         /*
          *string sourceDir = @"C:\Some\Directory";
 string filePattern = "*.txt";
 var quiesceTime = TimeSpan.FromMinutes(2);
 var dupeTime = TimeSpan.FromMinutes(5);

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
     // Do something
 } 
         
     } */

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
        /*public Program()
        {
            InitializeComponent();
        }*/

        protected override void OnStart(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            string sourceDir = @"C:\test";
            string filePattern = "*.*";
            var quiesceTime = TimeSpan.FromMinutes(0);
            var dupeTime = TimeSpan.FromMinutes(1);

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
                if(file != "C:\test\temp.txt")
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
