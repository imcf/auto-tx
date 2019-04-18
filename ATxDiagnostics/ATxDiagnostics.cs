using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ATxCommon;
using ATxCommon.Monitoring;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ATxDiagnostics
{
    internal class ATxDiagnostics
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static void Main(string[] args) {
            var loglevel = LogLevel.Debug;
            if (args.Length > 0 && args[0] == "trace") {
                loglevel = LogLevel.Trace;
            }
            var logConfig = new LoggingConfiguration();
            var logTargetConsole = new ConsoleTarget {
                Name = "console",
                Header = "AutoTx Diagnostics",
                Layout = @"${time} [${level}] ${message}",
            };
            logConfig.AddTarget(logTargetConsole);
            var logRuleConsole = new LoggingRule("*", loglevel, logTargetConsole);
            logConfig.LoggingRules.Add(logRuleConsole);
            LogManager.Configuration = logConfig;

            // set the default performance monitors:
            var perfMonitors = new[] {"CPU", "PhysicalDisk"};
            // if requested explicitly as a command line parameter, override them:
            if (args.Length > 1)
                perfMonitors = args[1].Split(',');

            var commonAssembly = Assembly.GetAssembly(typeof(Cpu));
            var commonVersionInfo = FileVersionInfo.GetVersionInfo(commonAssembly.Location);
            Log.Info("ATxCommon library version: {0}", commonVersionInfo.ProductVersion);

            Log.Debug("Free space on drive [C:]: " + Conv.BytesToString(SystemChecks.GetFreeDriveSpace("C:")));

            Log.Debug("\n\n>>>>>>>>>>>> running processes >>>>>>>>>>>>");
            foreach (var running in Process.GetProcesses()) {
                var title = running.MainWindowTitle;
                if (title.Length > 0) {
                    title = " (\"" + title + "\")";
                }
                Log.Debug(" - {0}{1}", running.ProcessName, title);
            }
            Log.Debug("\n<<<<<<<<<<<< running processes <<<<<<<<<<<<\n");

            if (perfMonitors.Contains("CPU")) {
                Log.Info("Watching CPU load using ATxCommon.Monitoring...");
                var cpu = new Cpu {
                    Interval = 250,
                    Limit = 50,
                    Probation = 4, // 4 * 250 ms = 1 second
                    LogPerformanceReadings = LogLevel.Info,
                    Enabled = true
                };
            }

            if (perfMonitors.Contains("PhysicalDisk")) {
                Log.Info("Watching I/O load using ATxCommon.Monitoring...");
                var disk = new PhysicalDisk {
                    Interval = 250,
                    Limit = 0.25F,
                    Probation = 4, // 4 * 250 ms = 1 second
                    LogPerformanceReadings = LogLevel.Info,
                    Enabled = true
                };
            }

            while (true) {
                System.Threading.Thread.Sleep(10000);   
            }
        }
    }
}
