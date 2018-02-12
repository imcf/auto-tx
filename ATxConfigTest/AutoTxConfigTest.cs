using System;
using System.IO;
using ATxCommon.Serializables;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ATxConfigTest
{
    internal class AutoTxConfigTest
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static ServiceConfig _config;
        private static ServiceStatus _status;

        private static void Main(string[] args) {
            var logConfig = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget {
                Name = "console",
                Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] (${logger}) ${message}",
            };
            logConfig.AddTarget("console", consoleTarget);
            var logRuleConsole = new LoggingRule("*", LogLevel.Debug, consoleTarget);
            logConfig.LoggingRules.Add(logRuleConsole);
            LogManager.Configuration = logConfig;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Length > 0)
                baseDir = args[0];

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
                Environment.ExitCode = -1;
            }
        }
    }
}
