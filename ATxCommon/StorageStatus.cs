using System;
using System.Collections.Generic;
using System.IO;
using ATxCommon.Serializables;
using NLog;

namespace ATxCommon
{
    public class StorageStatus
    {
        /// <summary>
        /// By default the status will only be updated if more than UpdateDelta seconds have
        /// elapsed since the last update to prevent too many updates causing high system load.
        /// </summary>
        public int UpdateDelta = 20;

        public DateTime LastStatusUpdate = DateTime.MinValue;

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, List<DirectoryDetails>> _expiredUserDirs;

        private readonly List<DriveToCheck> _drives;
        private readonly int _gracePeriod;
        private readonly string _gracePeriodHuman;
        private readonly DirectoryInfo _graceLocation;

        /// <summary>
        /// Initialize the StorageStatus object from the given ServiceConfig.
        /// </summary>
        /// <param name="config">The service configuration object.</param>
        public StorageStatus(ServiceConfig config) {
            _drives = config.SpaceMonitoring;
            _gracePeriod = config.GracePeriod;
            _gracePeriodHuman = config.HumanGracePeriod;
            _graceLocation = new DirectoryInfo(config.DonePath);
            _expiredUserDirs = new Dictionary<string, List<DirectoryDetails>>();
            Update();
        }

        /// <summary>
        /// Number of expired directories in the grace location.
        /// </summary>
        public int ExpiredUserDirsCount {
            get {
                Update();
                return _expiredUserDirs.Count;
            }
        }

        /// <summary>
        /// Get a dictionary of expired directories from the grace location.
        /// </summary>
        public Dictionary<string, List<DirectoryDetails>> ExpiredUserDirs {
            get {
                Update();
                return _expiredUserDirs;
            }
        }

        /// <summary>
        /// Human-friendly summary of expired directories in the grace location.
        /// </summary>
        /// <returns>A human-readable (i.e. formatted) string with details on the grace location
        /// and all expired directories, grouped by the topmost level (i.e. user dirs).</returns>
            Update();
        public string GraceLocationSummary() {
            var summary = "------ Grace location status, " +
                          $"threshold: {_gracePeriod} days ({_gracePeriodHuman}) ------\n\n" +
                          $" - location: [{_graceLocation}]\n";

            if (_expiredUserDirs.Count == 0)
                return summary + " -- NO EXPIRED folders in grace location! --";

            foreach (var dir in _expiredUserDirs.Keys) {
                summary += "\n - directory '" + dir + "'\n";
                foreach (var subdir in _expiredUserDirs[dir]) {
                    summary += $"    - {subdir.Dir.Name} " +
                               $"[age: {subdir.HumanAgeFromName}, " +
                               $"size: {subdir.HumanSize}]\n";
                }
            }

            return summary;
        }

        /// <summary>
        /// Human-friendly summary of free disk space on all configured drives.
        /// </summary>
        /// <param name="onlyLowSpace"></param>
        /// <returns>A human-readable (i.e. formatted) string with details on the free space on all
        /// configured drives. If <paramref name="onlyLowSpace"/> is set to "true", space will only
        /// be reported for drives that are below their corresponding threshold.</returns>
        public string SpaceSummary(bool onlyLowSpace = false) {
            Update();
            var summary = "------ Storage space status ------\n\n";
            foreach (var drive in _drives) {
                var msg = $" - drive [{drive.DriveName}] " +
                          $"free space: {Conv.BytesToString(drive.FreeSpace)} " +
                          $"(threshold: {Conv.GigabytesToString(drive.SpaceThreshold)})\n";

                if (onlyLowSpace && !drive.DiskSpaceLow()) {
                    Log.Trace(msg);
                    continue;
                }

                summary += msg;
            }

            return summary;
        }

        /// <summary>
        /// Update the current storage status in case the last update is already older than the
        /// configured threshold <see cref="LastStatusUpdate"/>.
        /// </summary>
        /// <param name="force">Update, independently of the last update timestamp.</param>
        public void Update(bool force = false) {
            if (force)
                LastStatusUpdate = DateTime.MinValue;

            if (TimeUtils.SecondsSince(LastStatusUpdate) < UpdateDelta)
                return;

            foreach (var userdir in _graceLocation.GetDirectories()) {
                var expired = new List<DirectoryDetails>();
                foreach (var subdir in userdir.GetDirectories()) {
                    var dirDetails = new DirectoryDetails(subdir);
                    if (dirDetails.AgeFromName < _gracePeriod)
                        continue;

                    expired.Add(dirDetails);
                }
                if (expired.Count > 0)
                    _expiredUserDirs.Add(userdir.Name, expired);

            }

            foreach (var drive in _drives) {
                try {
                    drive.FreeSpace = new DriveInfo(drive.DriveName).TotalFreeSpace;
                }
                catch (Exception ex) {
                    // log this as an error which then also gets sent via email (if configured) and
                    // let the rate-limiter take care of not flooding the admin with mails:
                    Log.Error("Error in GetFreeDriveSpace({0}): {1}", drive.DriveName, ex.Message);
                }
            }

            LastStatusUpdate = DateTime.Now;
        }

        /// <summary>
        /// Create an overall storage summary (free space and grace location).
        /// </summary>
        /// <returns>Human-readable string with details on free space + grace location.</returns>
        public string Summary() {
            return $"{SpaceSummary()}\n{GraceLocationSummary()}";
        }
    }
}
