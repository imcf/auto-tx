using System;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using ATxCommon;
using ATxCommon.Monitoring;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ATxDiagnostics
{
    class ATxDiagnostics
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args) {
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

            var commonAssembly = Assembly.GetAssembly(typeof(ATxCommon.Monitoring.Cpu));
            var commonVersionInfo = FileVersionInfo.GetVersionInfo(commonAssembly.Location);
            Log.Info("ATxCommon library version: {0}", commonVersionInfo.ProductVersion);

            Log.Debug("Free space on drive [C:]: " + Conv.BytesToString(SystemChecks.GetFreeDriveSpace("C:")));
            
            Log.Info("Checking CPU load using ATxCommon.Monitoring...");
            var cpu = new Cpu {
                Interval = 250,
                Limit = 25,
                Probation = 4,  // 4 * 250 ms = 1 second
                Enabled = true
            };
            System.Threading.Thread.Sleep(10000);
            cpu.Enabled = false;
            Log.Info("Finished checking CPU load using ATxCommon.Monitoring.\n");

            Log.Info("Checking CPU load using WMI...");
            for (int i = 0; i < 10; i++) {
                WmiQueryCpuLoad();
                System.Threading.Thread.Sleep(1000);
            }
            Log.Info("Finished checking CPU load using WMI.\n");
        }

        private static int WmiQueryCpuLoad() {
            Log.Trace("Querying WMI for CPU load...");
            var watch = Stopwatch.StartNew();
            var queryString = "SELECT Name, PercentProcessorTime " +
                              "FROM Win32_PerfFormattedData_PerfOS_Processor";
            var opts = new EnumerationOptions {
                Timeout = new TimeSpan(0, 0, 10),
                ReturnImmediately = false,
            };
            var searcher = new ManagementObjectSearcher("", queryString, opts);
            Int32 usageInt32 = -1;

            var managementObjects = searcher.Get();
            if (managementObjects.Count > 0) {
                Log.Trace("WMI query returned {0} objects.", managementObjects.Count);
                foreach (var mo in managementObjects) {
                    var obj = (ManagementObject)mo;
                    var usage = obj["PercentProcessorTime"];
                    var name = obj["Name"];

                    usageInt32 = Convert.ToInt32(usage);
                    Log.Debug("CPU usage {1}: {0}", usageInt32, name);

                }
            } else {
                Log.Error("No objects returned from WMI!");
            }

            managementObjects.Dispose();
            searcher.Dispose();

            watch.Stop();
            Log.Trace("WMI query took {0} ms.", watch.ElapsedMilliseconds);

            return usageInt32;
        }

    }
}
