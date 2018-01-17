using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NLog;

namespace ATXCommon
{
    public static class FsUtils
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Recursively sum up size of all files under a given path.
        /// </summary>
        /// <param name="path">Full path of the directory.</param>
        /// <returns>The total size in bytes.</returns>
        public static long GetDirectorySize(string path) {
            return new DirectoryInfo(path)
                .GetFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }

        /// <summary>
        /// Convert the timestamp given by the NAME of a directory into the age in days.
        /// </summary>
        /// <param name="dir">The DirectoryInfo object to check for its name-age.</param>
        /// <param name="baseTime">The DateTime object to compare to.</param>
        /// <returns>The age in days, or -1 in case of an error.</returns>
        public static int DirNameToAge(DirectoryInfo dir, DateTime baseTime) {
            DateTime dirTimestamp;
            try {
                dirTimestamp = DateTime.ParseExact(dir.Name, "yyyy-MM-dd__HH-mm-ss",
                    CultureInfo.InvariantCulture);
            }
            catch (Exception ex) {
                Log.Warn("Unable to parse time from name [{0}], skipping: {1}",
                    dir.Name, ex.Message);
                return -1;
            }
            return (baseTime - dirTimestamp).Days;
        }
    }
}
