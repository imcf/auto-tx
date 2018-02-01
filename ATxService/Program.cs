using System;
using System.ServiceProcess;
using System.IO;

namespace ATxService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            try
            {
                var ServicesToRun = new ServiceBase[]
                { 
                    new AutoTx() 
                };
                ServiceBase.Run(ServicesToRun);
            }
            catch (Exception ex)
            {
                var startupLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
                using (var sw = File.AppendText(startupLog))
                {
                    sw.WriteLine(ex.Message);
                }
                throw;
            }
                
        }
    }
}
