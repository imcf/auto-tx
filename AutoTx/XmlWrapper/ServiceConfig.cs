using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml.Serialization;

namespace AutoTx.XmlWrapper
{
    /// <summary>
    /// configuration class based on xml
    /// </summary>
    [Serializable]
    public class ServiceConfig
    {
        /// <summary>
        /// A human friendly name for the host, to be used in emails etc.
        /// </summary>
        public string HostAlias { get; set; }

        /// <summary>
        /// A human friendly name for the target, to be used in emails etc.
        /// </summary>
        public string DestinationAlias { get; set; }
        
        /// <summary>
        /// The base drive for the spooling directories (incoming and managed).
        /// </summary>
        public string SourceDrive { get; set; }

        /// <summary>
        /// The name of a directory on SourceDrive that is monitored for new files.
        /// </summary>
        public string IncomingDirectory { get; set; }

        /// <summary>
        /// The name of a marker file to be placed in all **sub**directories
        /// inside the IncomingDirectory.
        /// </summary>
        public string MarkerFile { get; set; }

        /// <summary>
        /// A directory on SourceDrive to hold the three subdirectories "DONE",
        /// "PROCESSING" and "UNMATCHED" used during and after transfers.
        /// </summary>
        public string ManagedDirectory { get; set; }
        
        /// <summary>
        /// Target path to transfer files to. Usually a UNC location.
        /// </summary>
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// The name of a subdirectory in the DestinationDirectory to be used
        /// to keep the temporary data of running transfers.
        /// </summary>
        public string TmpTransferDir { get; set; }
        public string SmtpHost { get; set; }
        public string SmtpUserCredential { get; set; }
        public string SmtpPasswortCredential { get; set; }
        public string EmailFrom { get; set; }
        public string AdminEmailAdress { get; set; }
        public string AdminDebugEmailAdress { get; set; }
        public string EmailPrefix { get; set; }

        public int ServiceTimer { get; set; }
        public int InterPacketGap { get; set; }
        public int MaxCpuUsage { get; set; }
        public int MinAvailableMemory { get; set; }
        public int SmtpPort { get; set; }
        public int AdminNotificationDelta { get; set; }
        public int StorageNotificationDelta { get; set; }

        /// <summary>
        /// GracePeriod: number of days after data in the "DONE" location expires,
        /// which will trigger a summary email to the admin address.
        /// </summary>
        public int GracePeriod { get; set; }

        public bool SendAdminNotification { get; set; }
        public bool SendTransferNotification { get; set; }
        public bool Debug { get; set; }

        [XmlArray]
        [XmlArrayItem(ElementName = "DriveToCheck")]
        public List<DriveToCheck> SpaceMonitoring { get; set; }

        [XmlArray]
        [XmlArrayItem(ElementName = "ProcessName")]
        public List<string> BlacklistedProcesses { get; set; }

        public static void Serialize(string file, ServiceConfig c) {
            // the config is never meant to be written by us, therefore:
            throw new SettingsPropertyIsReadOnlyException("The config file must not be written by the service!");
        }

        public static ServiceConfig Deserialize(string file) {
            var xs = new XmlSerializer(typeof(ServiceConfig));
            var reader = File.OpenText(file);
            var config = (ServiceConfig) xs.Deserialize(reader);
            reader.Close();
            return config;
        }

    }
}