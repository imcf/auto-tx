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

            const string mark = "----------------------------";

            try {
                Console.WriteLine($"\nTrying to parse configuration files from [{baseDir}]...\n");
                _config = ServiceConfig.Deserialize(baseDir);
                Console.WriteLine($"\n{mark} configuration settings {mark}");
                Console.Write(_config.Summary());
                Console.WriteLine($"{mark} configuration settings {mark}\n");
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                Environment.ExitCode = -1;
            }
        }
    }
}
