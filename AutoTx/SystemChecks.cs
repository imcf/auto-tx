using System;
using System.Diagnostics;
using ATXCommon;

namespace AutoTx
{
    public partial class AutoTx
    {
        /// <summary>
        /// Check all configured disks for their free space and send a notification
        /// if necessary (depending on the configured delta time).
        /// </summary>
        public void CheckFreeDiskSpace() {
            var msg = "";
            foreach (var driveToCheck in _config.SpaceMonitoring) {
                var freeSpace = SystemChecks.GetFreeDriveSpace(driveToCheck.DriveName);
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