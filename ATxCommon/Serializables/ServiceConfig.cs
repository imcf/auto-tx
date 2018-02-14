﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using NLog;

namespace ATxCommon.Serializables
{
    /// <summary>
    /// AutoTx service configuration class.
    /// </summary>
    [Serializable]
    public class ServiceConfig
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        #region required configuration parameters

        /// <summary>
        /// A human friendly name for the host, to be used in emails etc.
        /// </summary>
        public string HostAlias { get; set; }

        /// <summary>
        /// The base drive for the spooling directories (incoming and managed).
        /// </summary>
        public string SourceDrive { get; set; }

        /// <summary>
        /// The name of a directory on SourceDrive that is monitored for new files.
        /// </summary>
        public string IncomingDirectory { get; set; }

        /// <summary>
        /// A directory on SourceDrive to hold the three subdirectories "DONE",
        /// "PROCESSING" and "UNMATCHED" used during and after transfers.
        /// </summary>
        public string ManagedDirectory { get; set; }

        /// <summary>
        /// A human friendly name for the target, to be used in emails etc.
        /// </summary>
        public string DestinationAlias { get; set; }

        /// <summary>
        /// Target path to transfer files to. Usually a UNC location.
        /// </summary>
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// The name of a subdirectory in the DestinationDirectory to be used
        /// to keep the temporary data of running transfers.
        /// </summary>
        public string TmpTransferDir { get; set; }

        /// <summary>
        /// The interval (in ms) for checking for new files and system parameters.
        /// </summary>
        public int ServiceTimer { get; set; }

        /// <summary>
        /// Maximum allowed CPU usage across all cores in percent. Running transfers will be paused
        /// if this limit is exceeded.
        /// </summary>
        public int MaxCpuUsage { get; set; }

        /// <summary>
        /// Minimum amount of free RAM (in MB) required for the service to operate.
        /// </summary>
        public int MinAvailableMemory { get; set; }

        #endregion


        #region optional configuration parameters

        /// <summary>
        /// Switch on debug log messages.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// The name of a marker file to be placed in all **sub**directories
        /// inside the IncomingDirectory.
        /// </summary>
        public string MarkerFile { get; set; }

        /// <summary>
        /// SMTP server to send mails and Fatal/Error log messages. No mails if omitted.
        /// </summary>
        public string SmtpHost { get; set; }

        /// <summary>
        /// SMTP port for sending emails (default: 25).
        /// </summary>
        public int SmtpPort { get; set; }

        /// <summary>
        /// SMTP username to authenticate when sending emails (if required).
        /// </summary>
        public string SmtpUserCredential { get; set; }

        /// <summary>
        /// SMTP password to authenticate when sending emails (if required).
        /// </summary>
        public string SmtpPasswortCredential { get; set; }

        /// <summary>
        /// The email address to be used as "From:" when sending mail notifications.
        /// </summary>
        public string EmailFrom { get; set; }

        /// <summary>
        /// A string to be added as a prefix to the subject when sending emails.
        /// </summary>
        public string EmailPrefix { get; set; }

        /// <summary>
        /// The mail recipient address for admin notifications (including "Fatal" log messages).
        /// </summary>
        public string AdminEmailAdress { get; set; }
        
        /// <summary>
        /// The mail recipient address for debug notifications (including "Error" log messages).
        /// </summary>
        public string AdminDebugEmailAdress { get; set; }

        /// <summary>
        /// Flag whether to send a mail notification to the user upon completed transfers.
        /// </summary>
        public bool SendTransferNotification { get; set; }

        /// <summary>
        /// Flag whether to send explicit mail notifications to the admin on selected events.
        /// </summary>
        public bool SendAdminNotification { get; set; }

        /// <summary>
        /// Minimum time in minutes between two notifications to the admin, default: 60.
        /// </summary>
        public int AdminNotificationDelta { get; set; }

        /// <summary>
        /// Minimum time in minutes between two mails about expired folders in the grace location,
        /// default: 720 (12h).
        /// </summary>
        public int GraceNotificationDelta { get; set; }

        /// <summary>
        /// Minimum time in minutes between two low-space notifications, default: 720 (12h).
        /// </summary>
        public int StorageNotificationDelta { get; set; }

        /// <summary>
        /// GracePeriod: number of days after data in the "DONE" location expires,
        /// which will trigger a summary email to the admin address, default: 30.
        /// </summary>
        public int GracePeriod { get; set; }

        /// <summary>
        /// A list of process names causing transfers to be suspended if running.
        /// </summary>
        [XmlArray]
        [XmlArrayItem(ElementName = "ProcessName")]
        public List<string> BlacklistedProcesses { get; set; }

        /// <summary>
        /// EnforceInheritedACLs: whether to enforce ACL inheritance when moving files and
        /// directories, see https://support.microsoft.com/en-us/help/320246 for more details.
        /// </summary>
        public bool EnforceInheritedACLs { get; set; }

        /// <summary>
        /// A list of drives and thresholds to monitor free space.
        /// </summary>
        [XmlArray]
        [XmlArrayItem(ElementName = "DriveToCheck")]
        public List<DriveToCheck> SpaceMonitoring { get; set; }

        /// <summary>
        /// RoboCopy parameter for limiting the bandwidth (mostly for testing purposes).
        /// </summary>
        /// See the RoboCopy documentation for more details.
        public int InterPacketGap { get; set; }

        #endregion

        
        #region wrappers for derived parameters

        /// <summary>
        /// The full path to the incoming directory.
        /// </summary>
        [XmlIgnore]
        public string IncomingPath => Path.Combine(SourceDrive, IncomingDirectory);

        /// <summary>
        /// The full path to the managed directory.
        /// </summary>
        [XmlIgnore]
        public string ManagedPath => Path.Combine(SourceDrive, ManagedDirectory);

        /// <summary>
        /// The full path to the processing directory.
        /// </summary>
        [XmlIgnore]
        public string ProcessingPath => Path.Combine(ManagedPath, "PROCESSING");

        /// <summary>
        /// The full path to the done directory / grace location.
        /// </summary>
        [XmlIgnore]
        public string DonePath => Path.Combine(ManagedPath, "DONE");

        /// <summary>
        /// The full path to the directory for unmatched user directories.
        /// </summary>
        [XmlIgnore]
        public string UnmatchedPath => Path.Combine(ManagedPath, "UNMATCHED");

        #endregion


        /// <summary>
        /// Constructor setting default values for optional parameters.
        /// </summary>
        public ServiceConfig() {
            Log.Trace("ServiceConfig() constructor, setting defaults.");
            // set values for the optional XML elements (NOTE: parameters / variables that do not
            // strictly REQUIRE a value are listed here as comments to denote they have not just
            // been forgotten but an empty value is fine instead:

            Debug = false;
            // MarkerFile may be empty
            // SmtpHost may be empty
            SmtpPort = 25;
            // SmtpUserCredential may be empty
            // SmtpPasswortCredential may be empty
            // EmailFrom may be empty
            EmailPrefix = "[AutoTx Service] ";
            // AdminEmailAdress may be empty
            // AdminDebugEmailAdress may be empty
            SendTransferNotification = true;
            SendAdminNotification = true;
            AdminNotificationDelta = 60;
            GraceNotificationDelta = 720;
            StorageNotificationDelta = 720;
            GracePeriod = 30;
            // BlacklistedProcesses may be empty
            EnforceInheritedACLs = true;
            // SpaceMonitoring may be empty
            InterPacketGap = 0;
        }

        /// <summary>
        /// Dummy method raising an exception (this class must not be serialized).
        /// </summary>
        public static void Serialize(string file, ServiceConfig c) {
            // the config is never meant to be written by us, therefore:
            throw new SettingsPropertyIsReadOnlyException("The config file must not be written by the service!");
        }

        /// <summary>
        /// Load the host specific and the common XML configuration files, combine them and
        /// deserialize them into a ServiceConfig object. The host specific configuration file's
        /// name is defined as the hostname with an ".xml" suffix.
        /// </summary>
        /// <param name="path">The path to the configuration files.</param>
        /// <returns>A ServiceConfig object with validated settings.</returns>
        public static ServiceConfig Deserialize(string path) {
            ServiceConfig config;

            var commonFile = Path.Combine(path, "config.common.xml");
            var specificFile = Path.Combine(path, Environment.MachineName + ".xml");

            // for parsing the configuration from two separate files we are using the default
            // behaviour of the .NET XmlSerializer on duplicates: only the first occurrence is
            // used, all other ones are silentley being discarded - this way we simply append the
            // contents of the common config file to the host-specific and deserialize then:
            var common = XElement.Load(commonFile);
            Log.Debug("Loaded common configuration XML file: [{0}]", commonFile);
            var combined = XElement.Load(specificFile);
            Log.Debug("Loaded host specific configuration XML file: [{0}]", specificFile);
            combined.Add(common.Nodes());
            Log.Trace("Combined XML structure:\n\n{0}\n\n", combined);

            using (var reader = XmlReader.Create(new StringReader(combined.ToString()))) {
                Log.Debug("Trying to parse combined XML.");
                var serializer = new XmlSerializer(typeof(ServiceConfig));
                config = (ServiceConfig) serializer.Deserialize(reader);
            }

            ValidateConfiguration(config);
            Log.Debug("Successfully parsed and validated configuration XML.");
            return config;
        }

        /// <summary>
        /// Validate the configuration, throwing exceptions on invalid parameters.
        /// </summary>
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


            // NON-CRITICAL stuff is simply reported to the logs:
            if (!c.DestinationDirectory.StartsWith(@"\\")) {
                ReportNonOptimal("DestinationDirectory", c.DestinationDirectory, "is not a UNC path!");
            }
        }

        /// <summary>
        /// Print a standardized msg about a non-optimal configuration setting to the log.
        /// </summary>
        private static void ReportNonOptimal(string attribute, string value, string msg) {
            Log.Warn(">>> Non-optimal setting detected: <{0}> [{1}] {2}", attribute, value, msg);
        }

        /// <summary>
        /// Generate a human-readable sumary of the current configuration.
        /// </summary>
        /// <returns>A string with details on the configuration.</returns>
        public string Summary() {
            var msg =
                $"HostAlias: {HostAlias}\n" +
                $"SourceDrive: {SourceDrive}\n" +
                $"IncomingDirectory: {IncomingDirectory}\n" +
                $"MarkerFile: {MarkerFile}\n" +
                $"ManagedDirectory: {ManagedDirectory}\n" +
                $"GracePeriod: {GracePeriod} (" +
                TimeUtils.DaysToHuman(GracePeriod, false) + ")\n" +
                $"DestinationDirectory: {DestinationDirectory}\n" +
                $"TmpTransferDir: {TmpTransferDir}\n" +
                $"EnforceInheritedACLs: {EnforceInheritedACLs}\n" +
                $"ServiceTimer: {ServiceTimer} ms\n" +
                $"InterPacketGap: {InterPacketGap}\n" +
                $"MaxCpuUsage: {MaxCpuUsage}%\n" +
                $"MinAvailableMemory: {MinAvailableMemory}\n";
            foreach (var processName in BlacklistedProcesses) {
                msg += $"BlacklistedProcess: {processName}\n";
            }
            foreach (var drive in SpaceMonitoring) {
                msg += $"Drive to check free space: {drive.DriveName} " +
                       $"(threshold: {Conv.MegabytesToString(drive.SpaceThreshold)})\n";
            }
            if (string.IsNullOrEmpty(SmtpHost)) {
                msg += "SmtpHost: ====== Not configured, disabling email! ======" + "\n";
            } else {
                msg +=
                    $"SmtpHost: {SmtpHost}\n" +
                    $"SmtpUserCredential: {SmtpUserCredential}\n" +
                    $"EmailPrefix: {EmailPrefix}\n" +
                    $"EmailFrom: {EmailFrom}\n" +
                    $"AdminEmailAdress: {AdminEmailAdress}\n" +
                    $"AdminDebugEmailAdress: {AdminDebugEmailAdress}\n" +
                    $"StorageNotificationDelta: {StorageNotificationDelta} (" +
                    TimeUtils.MinutesToHuman(StorageNotificationDelta, false) + ")\n" +
                    $"AdminNotificationDelta: {AdminNotificationDelta} (" +
                    TimeUtils.MinutesToHuman(AdminNotificationDelta, false) + ")\n" +
                    $"GraceNotificationDelta: {GraceNotificationDelta} (" +
                    TimeUtils.MinutesToHuman(GraceNotificationDelta, false) + ")\n";
            }
            return msg;
        }
    }
}
