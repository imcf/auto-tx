namespace ATxCommon.Serializables
{
    /// <summary>
    /// Helper class for the nested SpaceMonitoring sections.
    /// </summary>
    public class DriveToCheck
    {
        /// <summary>
        /// A drive name (single letter followed by a colon, e.g. "D:") to be monitored for space.
        /// </summary>
        public string DriveName { get; set; }

        /// <summary>
        /// Limit (in GB) of free space, lower values will trigger a notification.
        /// </summary>
        public long SpaceThreshold { get; set; }
    }
}