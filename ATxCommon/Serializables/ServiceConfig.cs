using System;
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
        /// Maximum allowed CPU usage across all cores in percent. Running transfers will be paused
        /// if this limit is exceeded.
        /// </summary>
        public int MaxCpuUsage { get; set; }

        /// <summary>
        /// Maximum length of the disk queue multiplied by 1000 (so a value of "25" here means the
        /// queue length is required to be "0.025" or less). Running transfers will be paused if
        /// this limit is exceeded.
        /// </summary>
        public int MaxDiskQueue { get; set; }

        /// <summary>
        /// Minimum amount of free RAM (in MB) required for the service to operate.
        /// </summary>
        public int MinAvailableMemory { get; set; }

        #endregion


        #region optional configuration parameters

        /// <summary>
        /// NLog log level, one of "Warn", "Info", "Debug", "Trace". Default: "Info"
        /// </summary>
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// Log level to use for performance monitoring messages. Default: "Trace"
        /// </summary>
        public string LogLevelMonitoring { get; set; } = "Trace";

        /// <summary>
        /// Enable debug messages from the RoboSharp library. Default: false.
        /// </summary>
        public bool DebugRoboSharp { get; set; } = false;

        /// <summary>
        /// The full path of a file to be used for RoboCopy log messages. Default: "" (off).
        /// </summary>
        public string RoboCopyLog { get; set; } = "";

        /// <summary>
        /// The interval (in ms) for checking for new files and system parameters. Default: 1000.
        /// </summary>
        public int ServiceTimer { get; set; } = 1000;

        /// <summary>
        /// The name of a marker file to be placed in all **sub**directories
        /// inside the IncomingDirectory.
        /// </summary>
        public string MarkerFile { get; set; }

        /// <summary>
        /// Number of days after data in the "DONE" location expires. Default: 30.
        /// </summary>
        public int GracePeriod { get; set; } = 30;

        /// <summary>
        /// Whether to enforce ACL inheritance when moving files and directories, see 
        /// https://support.microsoft.com/en-us/help/320246 for more details. Default: false.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public bool EnforceInheritedACLs { get; set; } = false;

        /// <summary>
        /// Limit RoboCopy transfer bandwidth (mostly for testing purposes). Default: 0.
        /// </summary>
        /// See the RoboCopy documentation for more details.
        public int InterPacketGap { get; set; } = 0;

        /// <summary>
        /// Setting for the RoboCopy /COPY parameter, valid flags are D=Data, A=Attributes,
        /// T=Timestamps, S=Security(ACLs), O=Owner info, U=aUditing info. Default: "DT".
        /// </summary>
        public string CopyFlags { get; set; } = "DT";

        /// <summary>
        /// Setting for the RoboCopy /DCOPY parameter, valid flags depend on the version of
        /// RoboCopy used and should only be changed with greatest care! Default: "T".
        /// </summary>
        public string DirectoryCopyFlags { get; set; } = "T";

        /// <summary>
        /// A list of process names causing transfers to be suspended if running.
        /// </summary>
        [XmlArray]
        [XmlArrayItem(ElementName = "ProcessName")]
        public List<string> BlacklistedProcesses { get; set; }

        /// <summary>
        /// A list of drives and thresholds to monitor free space.
        /// </summary>
        [XmlArray]
        [XmlArrayItem(ElementName = "DriveToCheck")]
        public List<DriveToCheck> SpaceMonitoring { get; set; }

        #endregion


        #region optional configuration parameters - notification settings

        /// <summary>
        /// SMTP server to send mails and Fatal/Error log messages. No mails if omitted.
        /// </summary>
        public string SmtpHost { get; set; }

        /// <summary>
        /// SMTP port for sending emails. Default: 25.
        /// </summary>
        public int SmtpPort { get; set; } = 25;

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
        /// A prefix to be added to any email subject. Default: "[AutoTx Service] ".
        /// </summary>
        public string EmailPrefix { get; set; } = "[AutoTx Service] ";

        /// <summary>
        /// The mail recipient address for admin notifications (including "Fatal" log messages).
        /// </summary>
        public string AdminEmailAdress { get; set; }

        /// <summary>
        /// The mail recipient address for debug notifications (including "Error" log messages).
        /// </summary>
        public string AdminDebugEmailAdress { get; set; }

        /// <summary>
        /// Send an email to the user upon completed transfers. Default: true.
        /// </summary>
        public bool SendTransferNotification { get; set; } = true;

        /// <summary>
        /// Send email notifications to the admin on selected events. Default: true.
        /// </summary>
        public bool SendAdminNotification { get; set; } = true;

        /// <summary>
        /// Minimum time in minutes between two notifications to the admin. Default: 60.
        /// </summary>
        public int AdminNotificationDelta { get; set; } = 60;

        /// <summary>
        /// Minimum time in minutes between two mails about expired folders. Default: 720 (12h).
        /// </summary>
        public int GraceNotificationDelta { get; set; } = 720;

        /// <summary>
        /// Minimum time in minutes between two low-space notifications. Default: 720 (12h).
        /// </summary>
        public int StorageNotificationDelta { get; set; } = 720;
        
        /// <summary>
        /// Minimum time in minutes between two startup system health notifications.
        /// Default: 2880 (2d).
        /// </summary>
        public int StartupNotificationDelta { get; set; } = 2880;

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

        /// <summary>
        /// The full path to the directory for directories that were moved out of the way due to
        /// any kind of error in processing them.
        /// </summary>
        [XmlIgnore]
        public string ErrorPath => Path.Combine(ManagedPath, "ERROR");

        /// <summary>
        /// The LogLevel to be used for performance monitoring messages.
        /// </summary>
        [XmlIgnore]
        public LogLevel MonitoringLogLevel {
            get {
                switch (LogLevelMonitoring) {
                    case "Warn":
                        return global::NLog.LogLevel.Warn;
                    case "Info":
                        return global::NLog.LogLevel.Info;
                    case "Debug":
                        return global::NLog.LogLevel.Debug;
                    case "Trace":
                        return global::NLog.LogLevel.Trace;
                    default:
                        return global::NLog.LogLevel.Info;
                }
            }
        }

        [XmlIgnore]
        public static string ValidatorWarnings { get; set; }

        /// <summary>
        /// Convenience property converting the grace period to a human-friendly format.
        /// </summary>
        [XmlIgnore]
        public string HumanGracePeriod => TimeUtils.DaysToHuman(GracePeriod, false);

        #endregion


        /// <summary>
        /// ServiceConfig constructor, currently empty.
        /// </summary>
        private ServiceConfig() {
            Log.Trace("ServiceConfig() constructor.");
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
            Log.Trace("ServiceConfig.Deserialize({0})", path);
            ServiceConfig config;

            var commonFile = Path.Combine(path, "config.common.xml");
            var specificFile = Path.Combine(path, Environment.MachineName + ".xml");

            // for parsing the configuration from two separate files we are using the default
            // behaviour of the .NET XmlSerializer on duplicates: only the first occurrence is
            // used, all other ones are silentley being discarded - this way we simply append the
            // contents of the common config file to the host-specific and deserialize then:
            Log.Debug("Loading host specific configuration XML file: [{0}]", specificFile);
            var combined = XElement.Load(specificFile);
            // the common configuration file is optional, so check if it exists at all:
            if (File.Exists(commonFile)) {
                Log.Debug("Loading common configuration XML file: [{0}]", commonFile);
                var common = XElement.Load(commonFile);
                combined.Add(common.Nodes());
                Log.Trace("Combined XML structure:\n\n{0}\n\n", combined);
            }

            using (var reader = XmlReader.Create(new StringReader(combined.ToString()))) {
                Log.Debug("Trying to parse combined XML...");
                var serializer = new XmlSerializer(typeof(ServiceConfig));
                config = (ServiceConfig) serializer.Deserialize(reader);
            }

            ValidateConfiguration(config);
            
            Log.Debug("Successfully parsed a valid configuration from [{0}].", path);
            return config;
        }

        /// <summary>
        /// Validate the configuration, throwing exceptions on invalid parameters.
        /// </summary>
        private static void ValidateConfiguration(ServiceConfig c) {
            Log.Debug("Validating configuration...");
            var errmsg = "";

            string CheckEmpty(string value, string name) {
                // if the string is null terminate the validation immediately since this means the
                // file doesn't contain a required parameter at all:
                if (value == null) {
                    var msg = $"mandatory parameter missing: <{name}>";
                    Log.Error(msg);
                    throw new ConfigurationErrorsException(msg);
                }

                if (string.IsNullOrWhiteSpace(value))
                    return $"mandatory parameter unset: <{name}>\n";

                return string.Empty;
            }

            string CheckMinValue(int value, string name, int min) {
                if (value == 0)
                    return $"<{name}> is unset (or set to 0), minimal accepted value is {min}\n";

                if (value < min)
                    return $"<{name}> must not be smaller than {min}\n";

                return string.Empty;
            }

            string CheckLocalDrive(string value, string name) {
                var driveType = new DriveInfo(value).DriveType;
                if (driveType != DriveType.Fixed)
                    return $"<{name}> ({value}) must be a local fixed drive, not '{driveType}'!\n";
                return string.Empty;
            }

            void WarnOnHighValue(int value, string name, int thresh) {
                if (value > thresh)
                    SubOptimal(value.ToString(), name, "value is set very high, please check!");
            }

            void SubOptimal(string value, string name, string message) {
                var msg = $">>> Sub-optimal setting detected: <{name}> [{value}] {message}";
                ValidatorWarnings += msg + "\n";
                Log.Warn(msg);
            }

            void LogAndThrow(string msg) {
                msg = $"Configuration issues detected:\n{msg}";
                Log.Error(msg);
                throw new ConfigurationErrorsException(msg);
            }

            // check if all required parameters are there and non-empty / non-zero:
            errmsg += CheckEmpty(c.HostAlias, nameof(c.HostAlias));
            errmsg += CheckEmpty(c.SourceDrive, nameof(c.SourceDrive));
            errmsg += CheckEmpty(c.IncomingDirectory, nameof(c.IncomingDirectory));
            errmsg += CheckEmpty(c.ManagedDirectory, nameof(c.ManagedDirectory));
            errmsg += CheckEmpty(c.DestinationAlias, nameof(c.DestinationAlias));
            errmsg += CheckEmpty(c.DestinationDirectory, nameof(c.DestinationDirectory));
            errmsg += CheckEmpty(c.TmpTransferDir, nameof(c.TmpTransferDir));

            errmsg += CheckMinValue(c.ServiceTimer, nameof(c.ServiceTimer), 1000);
            errmsg += CheckMinValue(c.MaxCpuUsage, nameof(c.MaxCpuUsage), 5);
            errmsg += CheckMinValue(c.MaxDiskQueue, nameof(c.MaxDiskQueue), 1);
            errmsg += CheckMinValue(c.MinAvailableMemory, nameof(c.MinAvailableMemory), 256);

            // if any of the required parameter checks failed we terminate now as many of the
            // string checks below would fail on empty strings:
            if (!string.IsNullOrWhiteSpace(errmsg)) 
                LogAndThrow(errmsg);


            ////////// REQUIRED PARAMETERS SETTINGS VALIDATION //////////

            // SourceDrive
            if (c.SourceDrive.Substring(1) != @":\")
                errmsg += "<SourceDrive> must be of form [X:\\]\n!";
            errmsg += CheckLocalDrive(c.SourceDrive, nameof(c.SourceDrive));

            // spooling directories: IncomingDirectory + ManagedDirectory
            if (c.IncomingDirectory.StartsWith(@"\"))
                errmsg += "<IncomingDirectory> must not start with a backslash!\n";
            if (c.ManagedDirectory.StartsWith(@"\"))
                errmsg += "<ManagedDirectory> must not start with a backslash!\n";

            // DestinationDirectory
            if (!Directory.Exists(c.DestinationDirectory))
                errmsg += $"can't find (or reach) destination: {c.DestinationDirectory}\n";

            // TmpTransferDir
            var tmpTransferPath = Path.Combine(c.DestinationDirectory, c.TmpTransferDir);
            if (!Directory.Exists(tmpTransferPath))
                errmsg += $"can't find (or reach) temporary transfer dir: {tmpTransferPath}\n";


            ////////// OPTIONAL PARAMETERS SETTINGS VALIDATION //////////

            // EmailFrom
            if (!string.IsNullOrWhiteSpace(c.SmtpHost) &&
                string.IsNullOrWhiteSpace(c.EmailFrom))
                errmsg += "<EmailFrom> must not be empty if <SmtpHost> is configured!\n";
            
            // DriveName
            foreach (var driveToCheck in c.SpaceMonitoring) {
                errmsg += CheckLocalDrive(driveToCheck.DriveName, nameof(driveToCheck.DriveName));
            }


            ////////// WEAK CHECKS ON PARAMETERS SETTINGS //////////
            // those checks are non-critical and are simply reported to the logs

            WarnOnHighValue(c.ServiceTimer, nameof(c.ServiceTimer), 10000);
            WarnOnHighValue(c.MaxCpuUsage, nameof(c.MaxCpuUsage), 75);
            WarnOnHighValue(c.MaxDiskQueue, nameof(c.MaxDiskQueue), 2000);
            WarnOnHighValue(c.MinAvailableMemory, nameof(c.MinAvailableMemory), 8192);
            WarnOnHighValue(c.AdminNotificationDelta, nameof(c.AdminNotificationDelta), 1440);
            WarnOnHighValue(c.GraceNotificationDelta, nameof(c.GraceNotificationDelta), 10080);
            WarnOnHighValue(c.StorageNotificationDelta, nameof(c.StorageNotificationDelta), 10080);
            WarnOnHighValue(c.StartupNotificationDelta, nameof(c.StartupNotificationDelta), 40320);
            WarnOnHighValue(c.GracePeriod, nameof(c.GracePeriod), 100);

            if (!c.DestinationDirectory.StartsWith(@"\\"))
                SubOptimal(c.DestinationDirectory, "DestinationDirectory", "is not a UNC path!");

            // LogLevel
            var validLogLevels = new List<string> {"Warn", "Info", "Debug", "Trace"};
            if (!validLogLevels.Contains(c.LogLevel)) {
                SubOptimal(c.LogLevel, "LogLevel", "is invalid, using 'Debug'. Valid options: " +
                                                   string.Join(", ", validLogLevels));
                c.LogLevel = "Debug";
            }


            if (string.IsNullOrWhiteSpace(errmsg))
                return;

            LogAndThrow(errmsg);
        }

        /// <summary>
        /// Generate a human-readable sumary of the current configuration.
        /// </summary>
        /// <returns>A string with details on the configuration.</returns>
        public string Summary() {
            var msg =
                "############### REQUIRED PARAMETERS ###############\n" +
                $"HostAlias: {HostAlias}\n" +
                $"SourceDrive: {SourceDrive}\n" +
                $"IncomingDirectory: {IncomingDirectory}\n" +
                $"ManagedDirectory: {ManagedDirectory}\n" +
                $"DestinationAlias: {DestinationAlias}\n" +
                $"DestinationDirectory: {DestinationDirectory}\n" +
                $"TmpTransferDir: {TmpTransferDir}\n" +
                $"MaxCpuUsage: {MaxCpuUsage}%\n" +
                $"MaxDiskQueue: {MaxDiskQueue} / 1000 (effectively {(float)MaxDiskQueue/1000:0.000})\n" +
                $"MinAvailableMemory: {MinAvailableMemory} MB\n" +
                "\n" +
                "############### OPTIONAL PARAMETERS ###############\n" +
                $"LogLevel: {LogLevel}\n" +
                $"LogLevelMonitoring: {LogLevelMonitoring}\n" +
                $"ServiceTimer: {ServiceTimer} ms\n" +
                $"MarkerFile: {MarkerFile}\n" +
                $"GracePeriod: {GracePeriod} days (" +
                TimeUtils.DaysToHuman(GracePeriod, false) + ")\n" +
                $"EnforceInheritedACLs: {EnforceInheritedACLs}\n" +
                $"InterPacketGap: {InterPacketGap}\n" +
                "";

            var blacklist = "";
            foreach (var processName in BlacklistedProcesses) {
                blacklist += $"    ProcessName: {processName}\n";
            }
            if (!string.IsNullOrWhiteSpace(blacklist))
                msg += $"BlacklistedProcesses:\n{blacklist}";


            var space = "";
            foreach (var drive in SpaceMonitoring) {
                space += $"    DriveName: {drive.DriveName} " +
                       $"(threshold: {Conv.GigabytesToString(drive.SpaceThreshold)})\n";
            }
            if (!string.IsNullOrWhiteSpace(space))
                msg += $"SpaceMonitoring:\n{space}";

            if (string.IsNullOrWhiteSpace(SmtpHost)) {
                msg += "SmtpHost: ====== Not configured, disabling email! ======" + "\n";
            } else {
                msg +=
                    $"SmtpHost: {SmtpHost}\n" +
                    $"SmtpPort: {SmtpPort}\n" +
                    $"SmtpUserCredential: {SmtpUserCredential}\n" +
                    $"SmtpPasswortCredential: --- not showing ---\n" +
                    $"EmailFrom: {EmailFrom}\n" +
                    $"EmailPrefix: {EmailPrefix}\n" +
                    $"AdminEmailAdress: {AdminEmailAdress}\n" +
                    $"AdminDebugEmailAdress: {AdminDebugEmailAdress}\n" +
                    $"SendTransferNotification: {SendTransferNotification}\n" +
                    $"SendAdminNotification: {SendAdminNotification}\n" +
                    $"AdminNotificationDelta: {AdminNotificationDelta} min (" +
                    TimeUtils.MinutesToHuman(AdminNotificationDelta, false) + ")\n" +
                    $"GraceNotificationDelta: {GraceNotificationDelta} min (" +
                    TimeUtils.MinutesToHuman(GraceNotificationDelta, false) + ")\n" +
                    $"StorageNotificationDelta: {StorageNotificationDelta} min (" +
                    TimeUtils.MinutesToHuman(StorageNotificationDelta, false) + ")\n" +
                    $"StartupNotificationDelta: {StartupNotificationDelta} min (" +
                    TimeUtils.MinutesToHuman(StartupNotificationDelta, false) + ")\n" +
                    "";
            }
            return msg;
        }
    }
}
