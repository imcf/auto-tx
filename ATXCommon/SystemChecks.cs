using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using NLog;

namespace ATXCommon
{
    public static class SystemChecks
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Get the available physical memory in MB.
        /// </summary>
        /// <returns>The available physical memory in MB or -1 in case of an error.</returns>
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
                Log.Warn("Error in GetFreeMemory: {0}", ex.Message);
            }
            return -1;
        }

        /// <summary>
        /// Get the CPU usage in percent over all cores.
        /// </summary>
        /// <returns>CPU usage in percent or -1 if an error occured.</returns>
        public static int GetCpuUsage() {
            try {
                var searcher = new ManagementObjectSearcher("select * from Win32_PerfFormattedData_PerfOS_Processor");
                foreach (var mo in searcher.Get()) {
                    var obj = (ManagementObject)mo;
                    var usage = obj["PercentProcessorTime"];
                    var name = obj["Name"];
                    if (name.ToString().Equals("_Total"))
                        return Convert.ToInt32(usage);
                }
            }
            catch (Exception ex) {
                Log.Warn("Error in GetCpuUsage: {0}", ex.Message);
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
                Log.Warn("Error in GetFreeDriveSpace({0}): {1}", drive, ex.Message);
            }
            return 0;
        }

        /// <summary>
        /// Check all configured disks for their free space and send a notification
        /// if necessary (depending on the configured delta time).
        /// </summary>
        public static string CheckFreeDiskSpace(List<Serializables.DriveToCheck> drives) {
            var msg = "";
            foreach (var driveToCheck in drives) {
                var freeSpace = GetFreeDriveSpace(driveToCheck.DriveName);
                if (freeSpace >= driveToCheck.SpaceThreshold)
                    continue;

                msg += "Drive '" + driveToCheck.DriveName +
                       "' - free space: " + Conv.BytesToString(freeSpace) +
                       "  (threshold: " + Conv.BytesToString(driveToCheck.SpaceThreshold) + ")\n";
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
                    Log.Warn("Error in checkProcesses(): {0}", ex.Message);
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
                // TODO / FIXME: combine log and admin-email!
                var msg = string.Format("Error in getCurrentUsername(): {0}", ex.Message);
                Log.Error(msg);
                // TODO: FIXME!
                // SendAdminEmail(msg);
            }
            return username == "";
        }
    }
}
