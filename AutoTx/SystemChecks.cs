using System;
using System.Diagnostics;
using ATXCommon;

namespace AutoTx
{
    public partial class AutoTx
    {
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