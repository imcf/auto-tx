using System.Xml.Serialization;

namespace ATXSerializables
{
    /// <summary>
    /// Helper class for the nested SpaceMonitoring sections.
    /// </summary>
    public class DriveToCheck
    {
        [XmlElement("DriveName")]
        public string DriveName { get; set; }

        // the value is to be compared to System.IO.DriveInfo.TotalFreeSpace
        // hence we use the same type (long) to avoid unnecessary casts later:
        [XmlElement("SpaceThreshold")]
        public long SpaceThreshold { get; set; }
    }
}