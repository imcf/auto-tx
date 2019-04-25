using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using NLog;

namespace ATxCommon
{
    public static class SystemChecks
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Get the available physical memory in MB.
        /// </summary>
        /// <returns>Available physical memory in MB or -1 in case of an error.</returns>
        public static long GetFreeMemory() {
            try {
                var searcher =
                    new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (var mo in searcher.Get()) {
                    var queryObj = (ManagementObject)mo;
                    return Convert.ToInt64(queryObj["FreePhysicalMemory"]) / 1024;
                }
            }
            catch (Exception ex) {
                Log.Trace("Error in GetFreeMemory: {0}", ex.Message);
            }

            return -1;
        }

        /// <summary>
        /// Get the free space of a drive in bytes.
        /// </summary>
        /// /// <param name="drive">The drive name, e.g. "c:".</param>
        /// <returns>Free space of a drive in bytes, zero if an error occured.</returns>
        public static long GetFreeDriveSpace(string drive) {
            try {
                var dInfo = new DriveInfo(drive);
                return dInfo.TotalFreeSpace;
            }
            catch (Exception ex) {
                // log this as an error which then also gets sent via email (if configured) and
                // let the rate-limiter take care of not flooding the admin with mails:
                Log.Error("Error in GetFreeDriveSpace({0}): {1}", drive, ex.Message);
            }

            return 0;
        }

        /// <summary>
        /// Check all configured disks for their free space and generate a
        /// summary with details to be used in a notification message.
        /// </summary>
        public static string CheckFreeDiskSpace(List<Serializables.DriveToCheck> drives) {
            var msg = "";
            foreach (var driveToCheck in drives) {
                var freeSpace = GetFreeDriveSpace(driveToCheck.DriveName);
                if (freeSpace >= driveToCheck.SpaceThreshold * Conv.GigaBytes) {
                    Log.Trace("Drive [{0}] free space: {1}, above threshold ({2})",
                        driveToCheck.DriveName, Conv.BytesToString(freeSpace),
                        Conv.GigabytesToString(driveToCheck.SpaceThreshold));
                    continue;
                }

                msg += $"Drive [{driveToCheck.DriveName}] " +
                       $"free space: {Conv.BytesToString(freeSpace)} " +
                       $"(threshold: {Conv.GigabytesToString(driveToCheck.SpaceThreshold)})\n";
            }

            return msg;
        }

        /// <summary>
        /// Compares all processes against the ProcessNames in the BlacklistedProcesses list.
        /// </summary>
        /// <returns>Returns the name of the first matching process, an empty string otherwise.</returns>
        public static string CheckForBlacklistedProcesses(List<string> processNames) {
            foreach (var running in Process.GetProcesses()) {
                try {
                    foreach (var blacklisted in processNames) {
                        if (running.ProcessName.ToLower().Equals(blacklisted)) {
                            return blacklisted;
                        }
                    }
                }
                catch (Exception ex) {
                    Log.Error("Error in checkProcesses(): {0}", ex.Message);
                }
            }

            return "";
        }

        /// <summary>
        /// Check if a user is currently logged into Windows.
        /// 
        /// WARNING: this DOES NOT ACCOUNT for users logged in via RDP!!
        /// </summary>
        /// See https://stackoverflow.com/questions/5218778/ for the RDP problem.
        public static bool NoUserIsLoggedOn() {
            var username = "";
            try {
                var searcher = new ManagementObjectSearcher("SELECT UserName " +
                                                            "FROM Win32_ComputerSystem");
                var collection = searcher.Get();
                username = (string)collection.Cast<ManagementBaseObject>().First()["UserName"];
            }
            catch (Exception ex) {
                Log.Error("Error in getCurrentUsername(): {0}", ex.Message);
            }

            return username == "";
        }

        /// <summary>
        /// Log names of running processes if loglevel is set to debug.
        /// </summary>
        /// <param name="longFormat">By default only the process names will be printed to the
        /// log, enclosed by square brackets (e.g. [explorer]). If "longFormat" is set to true,
        /// each process name will be printed on a separate line, followed by the title of the
        /// corresponding main window (if existing).</param>
        public static void LogRunningProcesses(bool longFormat = false) {
            if (!Log.IsDebugEnabled)
                return;

            if (longFormat)
                Log.Debug("\n\n>>>>>>>>>>>> running processes >>>>>>>>>>>>");
            
            var procs = "";
            foreach (var running in Process.GetProcesses()) {
                if (longFormat) {
                    var title = running.MainWindowTitle;
                    if (title.Length > 0) {
                        title = " (\"" + title + "\")";
                    }
                    Log.Debug(" - {0}{1}", running.ProcessName, title);                    
                } else {
                    procs += $", [{running.ProcessName}]";
                }
            }

            if (longFormat) {
                Log.Debug("\n<<<<<<<<<<<< running processes <<<<<<<<<<<<\n");
            } else {
                Log.Debug("Currently running processes: {0}", procs.Substring(2));
            }
        }
    }
}
