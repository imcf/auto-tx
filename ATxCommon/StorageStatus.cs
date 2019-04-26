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
        /// By default the statuses will only be updated if more than UpdateDelta seconds have
        /// elapsed since the last update to prevent too many updates causing high system load.
        /// </summary>
        public int UpdateDelta = 20;

        private DateTime _lastUpdateFreeSpace = DateTime.MinValue;
        private DateTime _lastUpdateGraceLocation = DateTime.MinValue;

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, List<DirectoryDetails>> _expiredDirs;

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
            _expiredDirs = new Dictionary<string, List<DirectoryDetails>>();
            Log.Debug("StorageStatus initialization complete, updating status...");
            Update(true);
        }

        /// <summary>
        /// Number of expired directories in the grace location.
        /// </summary>
        public int ExpiredDirsCount {
            get {
                UpdateGraceLocation();
                return _expiredDirs.Count;
            }
        }

        /// <summary>
        /// Check if free space on all configured drives is above their threshold.
        /// </summary>
        /// <returns>False if any of the drives is below its threshold, true otherwise.</returns>
        public bool AllDrivesAboveThreshold() {
            UpdateFreeSpace();
            foreach (var drive in _drives) {
                if (drive.DiskSpaceLow()) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get a dictionary of expired directories from the grace location.
        /// </summary>
        public Dictionary<string, List<DirectoryDetails>> ExpiredDirs {
            get {
                UpdateGraceLocation();
                return _expiredDirs;
            }
        }

        /// <summary>
        /// Human-friendly summary of expired directories in the grace location.
        /// </summary>
        /// <returns>A human-readable (i.e. formatted) string with details on the grace location
        /// and all expired directories, grouped by the topmost level (i.e. user dirs).</returns>
        public string GraceLocationSummary() {
            UpdateGraceLocation();
            var summary = "------ Grace location status, " +
                          $"threshold: {_gracePeriod} days ({_gracePeriodHuman}) ------\n\n" +
                          $" - location: [{_graceLocation}]\n";

            if (_expiredDirs.Count == 0)
                return summary + " -- NO EXPIRED folders in grace location! --";

            foreach (var dir in _expiredDirs.Keys) {
                summary += "\n - directory '" + dir + "'\n";
                foreach (var subdir in _expiredDirs[dir]) {
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
            UpdateFreeSpace();
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
        /// Update the storage status of free drive space if it's older than its threshold.
        /// </summary>
        /// <param name="force">Update, independently of the last update timestamp.</param>
        public void UpdateFreeSpace(bool force = false) {
            if (force)
                _lastUpdateFreeSpace = DateTime.MinValue;

            if (TimeUtils.SecondsSince(_lastUpdateFreeSpace) < UpdateDelta)
                return;

            Log.Debug("Updating storage status: checking free disk space...");
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

            _lastUpdateFreeSpace = DateTime.Now;
        }

        /// <summary>
        /// Update the storage status of the grace location if it's older than its threshold.
        /// </summary>
        /// <param name="force">Update, independently of the last update timestamp.</param>
        public void UpdateGraceLocation(bool force = false) {
            if (force)
                _lastUpdateGraceLocation = DateTime.MinValue;

            if (TimeUtils.SecondsSince(_lastUpdateGraceLocation) < UpdateDelta)
                return;

            Log.Debug("Updating storage status: checking grace location...");
            _expiredDirs.Clear();
            foreach (var userdir in _graceLocation.GetDirectories()) {
                Log.Trace("Scanning directory [{0}]", userdir.Name);
                var expired = new List<DirectoryDetails>();
                foreach (var subdir in userdir.GetDirectories()) {
                    var dirDetails = new DirectoryDetails(subdir);
                    Log.Trace("Checking directory [{0}]: {1}",
                        dirDetails.Dir.Name, dirDetails.HumanAgeFromName);
                    if (dirDetails.AgeFromName < _gracePeriod)
                        continue;

                    Log.Trace("Found expired directory [{0}]", dirDetails.Dir.Name);
                    expired.Add(dirDetails);
                }
                Log.Trace("Found {0} expired dirs.", expired.Count);
                if (expired.Count > 0)
                    _expiredDirs.Add(userdir.Name, expired);
            }
            _lastUpdateGraceLocation = DateTime.Now;

            if (_expiredDirs.Count > 0) {
                Log.Debug("Updated storage status: {0} expired directories in grace location.",
                    _expiredDirs.Count);
            }
        }

        /// <summary>
        /// Update the current storage status in case the last update is already older than the
        /// configured threshold <see cref="_lastUpdateFreeSpace"/>.
        /// </summary>
        /// <param name="force">Update, independently of the last update timestamp.</param>
        public void Update(bool force = false) {
            try {
                UpdateFreeSpace(force);
                UpdateGraceLocation(force);
            }
            catch (Exception ex) {
                Log.Error("Updating storage status failed: {0}", ex.Message);
                throw;
            }
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
