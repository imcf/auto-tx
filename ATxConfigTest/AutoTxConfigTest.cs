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
        private static ServiceConfig _config;

        private static void Main(string[] args) {
            var logLevel = LogLevel.Info;
            var logPrefix = "";
            
            var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "var");
            if (args.Length > 0)
                baseDir = args[0];

            if (args.Length > 1) {
                if (args[1] == "debug") {
                    logLevel = LogLevel.Debug;
                    logPrefix = @"${date:format=yyyy-MM-dd HH\:mm\:ss} ";
                }
                if (args[1] == "trace") {
                    logLevel = LogLevel.Trace;
                    logPrefix = @"${date:format=yyyy-MM-dd HH\:mm\:ss} (${logger}) ";
                }
            }

            var logConfig = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget {
                Name = "console",
                Layout = logPrefix + @"[${level}] ${message}",
            };
            logConfig.AddTarget("console", consoleTarget);
            var logRuleConsole = new LoggingRule("*", logLevel, consoleTarget);
            logConfig.LoggingRules.Add(logRuleConsole);
            LogManager.Configuration = logConfig;

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
