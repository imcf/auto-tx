using System.Xml.Serialization;

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

        /// <summary>
        /// Free space of a drive in bytes, set to -1 if unknown or check resulted in an error.
        /// </summary>
        [XmlIgnore]
        public long FreeSpace { get; set; } = -1;

        /// <summary>
        /// Convenience method to check if this drive's free space is below its threshold.
        /// </summary>
        /// <returns>True if free space is below the threshold, false otherwise.</returns>
        public bool DiskSpaceLow() {
            return FreeSpace < SpaceThreshold * Conv.GigaBytes;
        }
    }
}