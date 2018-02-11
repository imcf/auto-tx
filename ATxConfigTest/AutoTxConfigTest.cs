using System;
using System.IO;
using ATxCommon.Serializables;
using NLog;
using NLog.Config;

namespace ATxConfigTest
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
                Console.WriteLine($"\nTrying to parse configuration file [{configPath}]...\n");
                _config = ServiceConfig.Deserialize(configPath);
                msg = "------------------ configuration settings ------------------";
                Console.WriteLine($"{msg}\n{_config.Summary()}{msg}\n");

                Console.WriteLine($"\nTrying to parse status file [{statusPath}]...\n");
                _status = ServiceStatus.Deserialize(statusPath, _config);
                msg = "------------------ status parameters ------------------";
                Console.WriteLine($"{msg}\n{_status.Summary()}{msg}\n");
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }
    }
}
