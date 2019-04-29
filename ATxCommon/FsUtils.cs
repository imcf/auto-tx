using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ATxCommon.Serializables;
using NLog;

namespace ATxCommon
{
    public static class FsUtils
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a directory with the given name if it doesn't exist yet, otherwise
        /// (optionally) create a new one using a date suffix to distinguish it from
        /// the existing one.
        /// </summary>
        /// <param name="dirPath">The full path of the directory to be created.</param>
        /// <param name="unique">Add a time-suffix to the name if the directory exists.</param>
        /// <returns>The name of the (created or pre-existing) directory. This will only
        /// differ from the input parameter "dirPath" if the "unique" parameter is set
        /// to true (then it will give the newly generated name) or if an error occured
        /// (in which case it will return an empty string).</returns>
        public static string CreateNewDirectory(string dirPath, bool unique) {
            try {
                if (Directory.Exists(dirPath)) {
                    // if unique was not requested, return the name of the existing dir:
                    if (unique == false)
                        return dirPath;

                    dirPath = dirPath + "_" + TimeUtils.Timestamp();
                }
                Directory.CreateDirectory(dirPath);
                Log.Debug("Created directory: [{0}]", dirPath);
                return dirPath;
            }
            catch (Exception ex) {
                Log.Error("Error in CreateNewDirectory({0}): {1}", dirPath, ex.Message);
            }
            return "";
        }

        /// <summary>
        /// Helper method to check if a directory exists, trying to create it if not.
        /// </summary>
        /// <param name="path">The full path of the directory to check / create.</param>
        /// <returns>True if existing or creation was successful, false otherwise.</returns>
        public static bool CheckForDirectory(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                Log.Error("ERROR: CheckForDirectory() parameter must not be empty!");
                return false;
            }
            return CreateNewDirectory(path, false) == path;
        }

        /// <summary>
        /// Recursively sum up size (in bytes) of all files under a given path.
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

        /// <summary>
        /// Collect individual files in the root of a given directory tree in a specific
        /// sub-directory. A file name given as "ignoredName" will be skipped in the checks.
        /// </summary>
        /// <param name="userDir">The user directory to check for individual files.</param>
        /// <param name="ignoredName">A filename that will be ignored.</param>
        public static void CollectOrphanedFiles(DirectoryInfo userDir, string ignoredName) {
            var fileList = userDir.GetFiles();
            var orphanedDir = Path.Combine(userDir.FullName, "orphaned");
            try {
                if (fileList.Length > 1 ||
                    (string.IsNullOrEmpty(ignoredName) && fileList.Length > 0)) {
                    if (Directory.Exists(orphanedDir)) {
                        Log.Info("Orphaned directory already exists, skipping individual files.");
                        return;
                    }
                    Log.Debug("Found individual files, collecting them in 'orphaned' folder.");
                    CreateNewDirectory(orphanedDir, false);
                }
                foreach (var file in fileList) {
                    if (file.Name.Equals(ignoredName))
                        continue;
                    Log.Debug("Collecting orphan: [{0}]", file.Name);
                    file.MoveTo(Path.Combine(orphanedDir, file.Name));
                }
            }
            catch (Exception ex) {
                Log.Error("Error collecting orphaned files: {0}\n{1}", ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        /// Ensure the required spooling directories (managed/incoming) exist.
        /// </summary>
        /// <param name="incoming">The path to the incoming location.</param>
        /// <param name="managed">The path to the managed location.</param>
        /// <returns>True if all dirs exist or were created successfully.</returns>
        public static bool CheckSpoolingDirectories(string incoming, string managed) {
            var retval = CheckForDirectory(incoming);
            retval &= CheckForDirectory(managed);
            retval &= CheckForDirectory(Path.Combine(managed, "PROCESSING"));
            retval &= CheckForDirectory(Path.Combine(managed, "DONE"));
            retval &= CheckForDirectory(Path.Combine(managed, "UNMATCHED"));
            retval &= CheckForDirectory(Path.Combine(managed, "ERROR"));
            return retval;
        }

        /// <summary>
        /// Helper to create directories for all users that have a dir in the local
        /// user directory (C:\Users) AND in the DestinationDirectory.
        /// </summary>
        /// <param name="dest">The full path to the destination directory, as given in
        /// <see cref="ServiceConfig.DestinationDirectory"/>.</param>
        /// <param name="tmp">The (relative) path to the temporary transfer location, as given in
        /// <see cref="ServiceConfig.TmpTransferDir"/>, commonly a single folder name.</param>
        /// <param name="incoming">The full path to the incoming directory, as given in
        /// <see cref="ServiceConfig.IncomingPath"/>.</param>
        /// <returns>The DateTime.Now object upon success, exceptions propagated otherwise.</returns>
        public static DateTime CreateIncomingDirectories(string dest, string tmp, string incoming) {
            var localUserDirs = new DirectoryInfo(@"C:\Users")
                .GetDirectories()
                .Select(d => d.Name.ToLower())
                .ToArray();
            var remoteUserDirs = new DirectoryInfo(dest)
                .GetDirectories()
                .Select(d => d.Name.ToLower())
                .ToArray();

            foreach (var userDir in localUserDirs) {
                // don't create an incoming directory for the same name as the
                // temporary transfer location:
                if (userDir == tmp)
                    continue;

                // don't create a directory if it doesn't exist on the target:
                if (!remoteUserDirs.Contains(userDir))
                    continue;

                CreateNewDirectory(Path.Combine(incoming, userDir), false);
            }
            return DateTime.Now;
        }

        /// <summary>
        /// Move all subdirectories of a given path into a destination directory. The destination
        /// will be created if it doesn't exist yet. If a subdirectory of the same name already
        /// exists in the destination, a timestamp-suffix is added to the new one.
        /// </summary>
        /// <param name="sourceDir">The source path as DirectoryInfo object.</param>
        /// <param name="destPath">The destination path as a string.</param>
        /// <param name="resetAcls">Whether to reset the ACLs on the moved subdirectories.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool MoveAllSubDirs(DirectoryInfo sourceDir, string destPath, bool resetAcls = false) {
            // TODO: check whether _transferState should be adjusted while moving dirs!
            Log.Debug("MoveAllSubDirs: [{0}] to [{1}]", sourceDir.FullName, destPath);
            try {
                // make sure the target directory that should hold all subdirectories to
                // be moved is existing:
                if (string.IsNullOrEmpty(CreateNewDirectory(destPath, false))) {
                    Log.Warn("WARNING: destination path doesn't exist: {0}", destPath);
                    return false;
                }

                foreach (var subDir in sourceDir.GetDirectories()) {
                    var target = Path.Combine(destPath, subDir.Name);
                    // make sure NOT to overwrite the subdirectories:
                    if (Directory.Exists(target))
                        target += "_" + TimeUtils.Timestamp();
                    Log.Debug(" - [{0}] > [{1}]", subDir.Name, target);
                    subDir.MoveTo(target);

                    if (!resetAcls)
                        continue;

                    try {
                        var acl = Directory.GetAccessControl(target);
                        acl.SetAccessRuleProtection(false, false);
                        Directory.SetAccessControl(target, acl);
                        Log.Debug("Successfully reset inherited ACLs on [{0}]", target);
                    }
                    catch (Exception ex) {
                        Log.Error("Error resetting inherited ACLs on [{0}]:\n{1}",
                            target, ex.Message);
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("Error moving directories: [{0}] -> [{1}]: {2}",
                    sourceDir.FullName, destPath, ex.Message);
                return false;
            }
            return true;
        }
    }
}
