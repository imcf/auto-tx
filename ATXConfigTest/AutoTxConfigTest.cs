using System;
using System.IO;
using ATXSerializables;

namespace ATXConfigTest
{
    internal class AutoTxConfigTest
    {

        private static ServiceConfig _config;
        private static ServiceStatus _status;

        private static void Main(string[] args) {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            try {
                baseDir = args[0];
            }
            catch {
                // ignored (use default value from above)
            }

            var configPath = Path.Combine(baseDir, "configuration.xml");
            var statusPath = Path.Combine(baseDir, "status.xml");

            try {
                string msg;
                Console.WriteLine("\nTrying to parse configuration file [{0}]...\n", configPath);
                _config = ServiceConfig.Deserialize(configPath);
                msg = "------------------ configuration settings ------------------";
                Console.WriteLine("{0}\n{1}{0}\n", msg, _config.Summary());

                Console.WriteLine("\nTrying to parse status file [{0}]...\n", statusPath);
                _status = ServiceStatus.Deserialize(statusPath, _config);
                msg = "------------------ status parameters ------------------";
                Console.WriteLine("{0}\n{1}{0}\n", msg, _status.Summary());
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }
    }
}
