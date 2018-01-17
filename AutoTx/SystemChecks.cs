using System;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace AutoTx
{
    public partial class AutoTx
    {

        /// <summary>
        /// Get the available physical memory in MB.
        /// </summary>
        /// <returns>The available physical memory in MB or -1 in case of an error.</returns>
        private long GetFreeMemory() {
            try {
                var searcher =
                    new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (var mo in searcher.Get()) {
                    var queryObj = (ManagementObject) mo;
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
        private int GetCpuUsage() {
            try {
                var searcher = new ManagementObjectSearcher("select * from Win32_PerfFormattedData_PerfOS_Processor");
                foreach (var mo in searcher.Get()) {
                    var obj = (ManagementObject) mo;
                    var usage = obj["PercentProcessorTime"];
                    var name = obj["Name"];
                    if (name.ToString().Equals("_Total")) return Convert.ToInt32(usage);
                }
            }
            catch (Exception ex) {
                Log.Warn("Error in GetCpuUsage: {0}", ex.Message);
            }
            return -1;
        }

        /// <summary>
        /// Get the free space of a drive in megabytes.
        /// </summary>
        /// /// <param name="drive">The drive name, e.g. "c:".</param>
        /// <returns>Free space of a drive in megabytes, zero if an error occured.</returns>
        private long GetFreeDriveSpace(string drive) {
            try {
                var dInfo = new DriveInfo(drive);
                return dInfo.TotalFreeSpace / MegaBytes;
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
        public void CheckFreeDiskSpace() {
            var msg = "";
            foreach (var driveToCheck in _config.SpaceMonitoring) {
                var freeSpace = GetFreeDriveSpace(driveToCheck.DriveName);
                if (freeSpace >= driveToCheck.SpaceThreshold) continue;

                msg += "Drive '" + driveToCheck.DriveName +
                       "' - free space: " + freeSpace +
                       "  (threshold: " + driveToCheck.SpaceThreshold + ")\n";
            }
            if (msg != "")
                SendLowSpaceMail(msg);
        }

        /// <summary>
        /// Compares all processes against the ProcessNames in the BlacklistedProcesses list.
        /// </summary>
        /// <returns>Returns the name of the first matching process, an empty string otherwise.</returns>
        public string CheckForBlacklistedProcesses() {
            foreach (var running in Process.GetProcesses()) {
                try {
                    foreach (var blacklisted in _config.BlacklistedProcesses) {
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
    }
}