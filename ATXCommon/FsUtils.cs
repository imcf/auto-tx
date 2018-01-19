using System;
using System.Collections.Generic;
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
                // TODO: discuss if this should be an "Error" message to trigger a mail
                // notification to the AdminDebug address:
                Log.Warn("Unable to parse time from name [{0}], skipping: {1}",
                    dir.Name, ex.Message);
                return -1;
            }
            return (baseTime - dirTimestamp).Days;
        }

        /// <summary>
        /// Assemble a dictionary with information about expired directories.
        /// </summary>
        /// <param name="baseDir">The base directory to scan for subdirectories.</param>
        /// <param name="thresh">The number of days used as expiration threshold.</param>
        /// <returns>A dictionary having usernames as keys (of those users that actually do have
        /// expired directories), where the values are lists of tuples with the DirInfo objects,
        /// size (in bytes) and age (in days) of the expired directories.</returns>
        public static Dictionary<string, List<Tuple<DirectoryInfo, long, int>>>
            ExpiredDirs(DirectoryInfo baseDir,int thresh) {

            var collection = new Dictionary<string, List<Tuple<DirectoryInfo, long, int>>>();
            var now = DateTime.Now;
            foreach (var userdir in baseDir.GetDirectories()) {
                var expired = new List<Tuple<DirectoryInfo, long, int>>();
                foreach (var subdir in userdir.GetDirectories()) {
                    var age = DirNameToAge(subdir, now);
                    if (age < thresh)
                        continue;
                    long size = -1;
                    try {
                        size = GetDirectorySize(subdir.FullName);
                    }
                    catch (Exception ex) {
                        Log.Error("ERROR getting directory size of [{0}]: {1}",
                            subdir.FullName, ex.Message);
                    }
                    expired.Add(new Tuple<DirectoryInfo, long, int>(subdir, size, age));
                }
                if (expired.Count > 0)
                    collection.Add(userdir.Name, expired);
            }
            return collection;
        }

        /// <summary>
        /// Check if a given directory is empty. If a marker file is set in the config a
        /// file with this name will be created inside the given directory and will be
        /// skipped itself when checking for files and directories.
        /// </summary>
        /// <param name="dirInfo">The directory to check.</param>
        /// <param name="ignoredName">A filename that will be ignored.</param>
        /// <returns>True if access is denied or the dir is empty, false otherwise.</returns>
        public static bool DirEmptyExcept(DirectoryInfo dirInfo, string ignoredName) {
            try {
                var filesInTree = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                if (string.IsNullOrEmpty(ignoredName))
                    return filesInTree.Length == 0;

                // check if there is ONLY the marker file:
                if (filesInTree.Length == 1 &&
                    filesInTree[0].Name.Equals(ignoredName))
                    return true;

                // make sure the marker file is there:
                var markerFilePath = Path.Combine(dirInfo.FullName, ignoredName);
                if (!File.Exists(markerFilePath))
                    File.Create(markerFilePath);

                return filesInTree.Length == 0;
            }
            catch (Exception e) {
                Log.Error("Error accessing directories: {0}", e.Message);
            }
            // if nothing triggered before, we pretend the dir is empty:
            return true;
        }
    }
}
