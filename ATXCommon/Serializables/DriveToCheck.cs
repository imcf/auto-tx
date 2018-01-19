using System.Xml.Serialization;

namespace ATXCommon.Serializables
{
    /// <summary>
    /// Helper class for the nested SpaceMonitoring sections.
    /// </summary>
    public class DriveToCheck
    {
        /// <summary>
        /// A drive name (single letter followed by a colon, e.g. "D:") to be monitored for space.
        /// </summary>
        [XmlElement("DriveName")]
        public string DriveName { get; set; }

        /// <summary>
        /// Limit (in MB) of free space, lower values will trigger a notification.
        /// </summary>
        /// Value is to be compared to DriveInfo.TotalFreeSpace, hence the same type (long).
        [XmlElement("SpaceThreshold")]
        public long SpaceThreshold { get; set; }
    }
}