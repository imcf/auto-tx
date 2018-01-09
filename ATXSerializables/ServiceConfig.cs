using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml.Serialization;

namespace ATXSerializables
{
    /// <summary>
    /// configuration class based on xml
    /// </summary>
    [Serializable]
    public class ServiceConfig
    {
        [XmlIgnore] public string ValidationWarnings;

        public ServiceConfig() {
            ValidationWarnings = "";
            // set values for the optional XML elements:
            SmtpHost = "";
            SmtpPort = 25;
            SmtpUserCredential = "";
            SmtpPasswortCredential = "";
            EmailPrefix = "";
            AdminEmailAdress = "";
            AdminDebugEmailAdress = "";
            GraceNotificationDelta = 720;

            InterPacketGap = 0;

            EnforceInheritedACLs = true;
        }

        #region required configuration parameters

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

        public string EmailFrom { get; set; }

        public int ServiceTimer { get; set; }

        public int MaxCpuUsage { get; set; }
        public int MinAvailableMemory { get; set; }
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

        #endregion


        #region optional configuration parameters

        public string SmtpHost { get; set; }
        public string SmtpUserCredential { get; set; }
        public string SmtpPasswortCredential { get; set; }
        public int SmtpPort { get; set; }

        public string EmailPrefix { get; set; }
        public string AdminEmailAdress { get; set; }
        public string AdminDebugEmailAdress { get; set; }

        public int GraceNotificationDelta { get; set; }

        public int InterPacketGap { get; set; }

        /// <summary>
        /// EnforceInheritedACLs: whether to enforce ACL inheritance when moving files and
        /// directories, see https://support.microsoft.com/en-us/help/320246 for more details.
        /// </summary>
        public bool EnforceInheritedACLs { get; set; }

        #endregion


        public static void Serialize(string file, ServiceConfig c) {
            // the config is never meant to be written by us, therefore:
            throw new SettingsPropertyIsReadOnlyException("The config file must not be written by the service!");
        }

        public static ServiceConfig Deserialize(string file) {
            var xs = new XmlSerializer(typeof(ServiceConfig));
            var reader = File.OpenText(file);
            var config = (ServiceConfig) xs.Deserialize(reader);
            reader.Close();
            ValidateConfiguration(config);
            return config;
        }

        private static void ValidateConfiguration(ServiceConfig c) {
            if (string.IsNullOrEmpty(c.SourceDrive) ||
                string.IsNullOrEmpty(c.IncomingDirectory) ||
                string.IsNullOrEmpty(c.ManagedDirectory))
                throw new ConfigurationErrorsException("mandatory parameter missing!");

            if (c.SourceDrive.Substring(1) != @":\")
                throw new ConfigurationErrorsException("SourceDrive must be a drive " +
                                                       @"letter followed by a colon and a backslash, e.g. 'D:\'!");

            // make sure SourceDrive is a local (fixed) disk:
            var driveInfo = new DriveInfo(c.SourceDrive);
            if (driveInfo.DriveType != DriveType.Fixed)
                throw new ConfigurationErrorsException("SourceDrive (" + c.SourceDrive +
                                                       ") must be a local (fixed) drive, OS reports '" +
                                                       driveInfo.DriveType + "')!");


            // spooling directories: IncomingDirectory + ManagedDirectory
            if (c.IncomingDirectory.StartsWith(@"\"))
                throw new ConfigurationErrorsException("IncomingDirectory must not start with a backslash!");
            if (c.ManagedDirectory.StartsWith(@"\"))
                throw new ConfigurationErrorsException("ManagedDirectory must not start with a backslash!");

            if (!Directory.Exists(c.DestinationDirectory))
                throw new ConfigurationErrorsException("can't find destination: " + c.DestinationDirectory);

            var tmpTransferPath = Path.Combine(c.DestinationDirectory, c.TmpTransferDir);
            if (!Directory.Exists(tmpTransferPath))
                throw new ConfigurationErrorsException("temporary transfer dir doesn't exist: " + tmpTransferPath);

            if (c.ServiceTimer < 1000)
                throw new ConfigurationErrorsException("ServiceTimer must not be smaller than 1000 ms!");


            // NON-CRITICAL stuff just adds messages to ValidationWarnings:
            // DestinationDirectory
            if (!c.DestinationDirectory.StartsWith(@"\\"))
                c.ValidationWarnings += " - <DestinationDirectory> is not a UNC path!\n";
        }

        public string Summary() {
            var msg =
                "HostAlias: " + HostAlias + "\n" +
                "SourceDrive: " + SourceDrive + "\n" +
                "IncomingDirectory: " + IncomingDirectory + "\n" +
                "MarkerFile: " + MarkerFile + "\n" +
                "ManagedDirectory: " + ManagedDirectory + "\n" +
                "GracePeriod: " + GracePeriod + "\n" +
                "DestinationDirectory: " + DestinationDirectory + "\n" +
                "TmpTransferDir: " + TmpTransferDir + "\n" +
                "EnforceInheritedACLs: " + EnforceInheritedACLs + "\n" +
                "ServiceTimer: " + ServiceTimer + "\n" +
                "InterPacketGap: " + InterPacketGap + "\n" +
                "MaxCpuUsage: " + MaxCpuUsage + "\n" +
                "MinAvailableMemory: " + MinAvailableMemory + "\n";
            foreach (var processName in BlacklistedProcesses) {
                msg += "BlacklistedProcess: " + processName + "\n";
            }
            foreach (var driveToCheck in SpaceMonitoring) {
                msg += "Drive to check free space: " + driveToCheck.DriveName +
                       " (threshold: " + driveToCheck.SpaceThreshold + ")" + "\n";
            }
            if (string.IsNullOrEmpty(SmtpHost)) {
                msg += "SmtpHost: ====== Not configured, disabling email! ======" + "\n";
            } else {
                msg +=
                    "SmtpHost: " + SmtpHost + "\n" +
                    "EmailFrom: " + EmailFrom + "\n" +
                    "AdminEmailAdress: " + AdminEmailAdress + "\n" +
                    "AdminDebugEmailAdress: " + AdminDebugEmailAdress + "\n" +
                    "StorageNotificationDelta: " + StorageNotificationDelta + "\n" +
                    "AdminNotificationDelta: " + AdminNotificationDelta + "\n" +
                    "GraceNotificationDelta: " + GraceNotificationDelta + "\n";
            }
            return msg;
        }
    }
}
