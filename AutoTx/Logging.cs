using System;
using System.IO;

namespace AutoTx
{
    public partial class AutoTx
    {
        #region global variables

        private string _logPath;

        #endregion

        /// <summary>
        /// Write a message to the log file and optionally send a mail to the admin address.
        /// </summary>
        /// <param name="message">The text to write into the log.</param>
        /// <param name="notify">Send the log message to the admin email as well.</param>
        public void writeLog(string message, bool notify = false) {
            message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ": " + message;
            using (var sw = File.AppendText(_logPath)) {
                sw.WriteLine(message);
            }
            if (notify)
                SendAdminEmail(message);
        }

        /// <summary>
        /// Call writeLog() if debug mode is enabled, optionally send an admin notification.
        /// </summary>
        public void writeLogDebug(string logText, bool notify = false) {
            if (_config.Debug)
                writeLog("[DEBUG] " + logText, notify);
        }

        /// <summary>
        /// Check the size of the existing logfiles and trigger rotation if necessary.
        /// </summary>
        public void CheckLogSize() {
            const long logSizeLimit = 1000000; // maximum log file size in bytes
            FileInfo logInfo;
            try {
                logInfo = new FileInfo(_logPath);
            }
            catch (Exception ex) {
                writeLog(ex.Message);
                return;
            }
            if (logInfo.Length <= logSizeLimit) return;
            try {
                RotateLogFiles("log file size above threshold: " + logInfo.Length + " bytes\n");
            }
            catch (Exception ex) {
                writeLog(ex.Message);
            }
        }

        /// <summary>
        /// Rotate the existing logfiles.
        /// </summary>
        /// <param name="message"></param>
        public void RotateLogFiles(string message) {
            const int maxToKeep = 10; // number of log files to keep

            for (var i = maxToKeep - 2; i >= 0; i--) {
                var curName = _logPath + "." + i;
                if (i == 0) curName = _logPath; // the current logfile (i==0) should not have a suffix
                var newName = _logPath + "." + (i + 1);
                if (!File.Exists(curName)) continue;
                if (File.Exists(newName)) {
                    message += "deleting: " + newName + "\n";
                    File.Delete(newName);
                }
                message += "moving " + curName + " to " + newName + "\n";
                File.Move(curName, newName);
            }
            // This will re-create the current log file:
            writeLogDebug(message);
        }
    }
}