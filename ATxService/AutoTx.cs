﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.ServiceProcess;
using System.IO;
using System.Reflection;
using System.Timers;
using ATxCommon;
using ATxCommon.Monitoring;
using ATxCommon.NLog;
using ATxCommon.Serializables;
using NLog;
using NLog.Config;
using NLog.Targets;
using RoboSharp;
using Debugger = RoboSharp.Debugger;

// NOTE on naming conventions: variables containing "Path" are strings, variables containing
// "Dir" are DirectoryInfo objects!

namespace ATxService
{
    public partial class AutoTx : ServiceBase
    {
        #region global variables

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private const string LogFormatDefault = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] ${message}";
        // private const string LogFormatDefault = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] (${logger}) ${message}"

        private static string _versionSummary;

        private readonly List<string> _transferredFiles = new List<string>();
        
        /// <summary>
        /// A list of (full) paths to be ignored in the incoming location, that will be populated
        /// during runtime only, i.e. will not persist over a service restart (intentionally!).
        /// </summary>
        private readonly List<string> _incomingIgnore = new List<string>();

        /// <summary>
        /// The CPU load monitoring object.
        /// </summary>
        private Cpu _cpu;

        /// <summary>
        /// The Disk I/O monitoring object.
        /// </summary>
        private PhysicalDisk _phyDisk;

        private RoboCommand _roboCommand;
        
        /// <summary>
        /// Size of the file currently being transferred (in bytes). Zero if no transfer running.
        /// </summary>
        private long _txCurFileSize;

        /// <summary>
        /// Progress (in percent) of the file currently being transferred. Zero if no transfer.
        /// </summary>
        private int _txCurFileProgress;

        /// <summary>
        /// Internal counter to introduce a delay between two subsequent transfers.
        /// </summary>
        private int _waitCyclesBeforeNextTx;

        /// <summary>
        /// Counter on how many load monitoring properties are currently exceeding their limit(s).
        /// </summary>
        // ReSharper disable once RedundantDefaultMemberInitializer
        private int _exceedingLoadLimit = 0;

        private DateTime _lastUserDirCheck = DateTime.MinValue;

        /// <summary>
        /// The transfer state, one of "Stopped", "Active", "Paused" or "DoNothing".
        /// 
        /// Stopped:   The last transfer was finished successfully or none was started yet. A new
        ///            transfer MAY ONLY BE STARTED if the service is in this state.
        /// 
        /// Active:    A transfer is currently running (i.e. new transfers MUST NOT be started).
        /// 
        /// Paused:    Assigned in PauseTransfer() which gets called from within RunMainTasks()
        ///            when system parameters are not in their valid range. It gets evaluated if
        ///            the parameters return to valid or if no user is logged on any more.
        /// 
        /// DoNothing: Assigned when the service gets shut down (in the OnStop() method) to prevent
        ///            accidentially launching new transfers or doing other tasks.
        /// </summary>
        private enum TxState
        {
            Stopped = 0,
            Active = 1,
            Paused = 2,
            DoNothing = 3
        }

        private TxState _transferState = TxState.Stopped;

        private ServiceConfig _config;
        private ServiceStatus _status;
        private StorageStatus _storage;

        private static Timer _mainTimer;

        #endregion

        #region initialize, load and check configuration + status

        /// <inheritdoc />
        /// <summary>
        /// AutoTx constructor
        /// </summary>
        public AutoTx() {
            // make sure to receive system shutdown events to react properly:
            CanShutdown = true;

            InitializeComponent();
            SetupFileLogging();
            Log.Info("=".PadLeft(80, '='));
            Log.Info("Attempting to start {0} service...", ServiceName);
            CreateEventLog();
            LoadSettings();

            InitializePerformanceMonitors();
            InitializeDirectories();
            SetupStorageStatus();
            StartupSummary();

            if (_config.DebugRoboSharp) {
                Debugger.Instance.DebugMessageEvent += HandleDebugMessage;
                Log.Debug("Enabled RoboSharp debug logging.");
            }
        }

        /// <summary>
        /// Configure NLog logging for the file target.
        /// </summary>
        private void SetupFileLogging(string logLevelName = "Debug") {
            LogLevel logLevel;
            switch (logLevelName) {
                case "Warn":
                    logLevel = LogLevel.Warn;
                    break;
                case "Info":
                    logLevel = LogLevel.Info;
                    break;
                case "Trace":
                    logLevel = LogLevel.Trace;
                    break;
                default:
                    logLevel = LogLevel.Debug;
                    break;
            }
            var logConfig = new LoggingConfiguration();
            var fileTarget = new FileTarget {
                Name = "file",
                FileName = $"var/{Environment.MachineName}.{ServiceName}.log",
                ArchiveAboveSize = 1 * Conv.MegaBytes,
                ArchiveFileName = $"var/{Environment.MachineName}.{ServiceName}" + ".{#}.log",
                MaxArchiveFiles = 9,
                KeepFileOpen = true,
                Layout = LogFormatDefault,
            };
            logConfig.AddTarget("file", fileTarget);
            var logRuleFile = new LoggingRule("*", logLevel, fileTarget);
            logConfig.LoggingRules.Add(logRuleFile);
            LogManager.Configuration = logConfig;
        }

        /// <summary>
        /// Configure logging to email targets.
        /// 
        /// Depending on the configuration, set up the logging via email. If no SmtpHost or no
        /// AdminEmailAddress is configured, nothing will be done. If they're set in the config file,
        /// a log target for messages with level "Fatal" will be configured. In addition, if the
        /// AdminDebugEmailAddress is set, another target for "Error" level messages is configured
        /// using this address as recipient.
        /// </summary>
        private void SetupMailLogging() {
            try {
                if (string.IsNullOrWhiteSpace(_config.SmtpHost) ||
                    string.IsNullOrWhiteSpace(_config.AdminEmailAddress)) {
                    Log.Info("SMTP host or admin recipient unconfigured, disabling mail logging.");
                    return;
                }

                var subject = $"{ServiceName} - {_config.HostAlias} - Admin Notification";
                var body = $"Notification from '{_config.HostAlias}' [{Environment.MachineName}] (via NLog)\n\n" +
                           $"{LogFormatDefault}\n\n" +
                           "NOTE: messages of the same log level won't be sent via email for the\n" +
                           $"next {_config.AdminNotificationDelta} minutes (or a service restart)," +
                           "please check the corresponding log file!";

                var logConfig = LogManager.Configuration;

                // "Fatal" target
                var mailTargetFatal = new MailTarget {
                    Name = "mailfatal",
                    SmtpServer = _config.SmtpHost,
                    SmtpPort = _config.SmtpPort,
                    From = _config.EmailFrom,
                    To = _config.AdminEmailAddress,
                    Subject = subject,
                    Body = body,
                };
                var mailTargetFatalLimited = new RateLimitWrapper {
                    Name = "mailfatallimited",
                    MinLogInterval = _config.AdminNotificationDelta,
                    WrappedTarget = mailTargetFatal
                };
                logConfig.AddTarget(mailTargetFatalLimited);
                logConfig.AddRuleForOneLevel(LogLevel.Fatal, mailTargetFatalLimited);

                // "Error" target
                if (!string.IsNullOrWhiteSpace(_config.AdminDebugEmailAddress)) {
                    var mailTargetError = new MailTarget {
                        Name = "mailerror",
                        SmtpServer = _config.SmtpHost,
                        SmtpPort = _config.SmtpPort,
                        From = _config.EmailFrom,
                        To = _config.AdminDebugEmailAddress,
                        Subject = subject,
                        Body = body,
                    };
                    var mailTargetErrorLimited = new RateLimitWrapper {
                        Name = "mailerrorlimited",
                        MinLogInterval = _config.AdminNotificationDelta,
                        WrappedTarget = mailTargetError
                    };
                    logConfig.AddTarget(mailTargetErrorLimited);
                    logConfig.AddRuleForOneLevel(LogLevel.Error, mailTargetErrorLimited);
                    Log.Info("Configured mail notification for 'Error' messages to {0}", mailTargetError.To);
                }
                Log.Info("Configured mail notification for 'Fatal' messages to {0}", mailTargetFatal.To);
                LogManager.Configuration = logConfig;
            }
            catch (Exception ex) {
                Log.Error("SetupMailLogging(): {0}", ex.Message);
            }
        }

        /// <summary>
        /// Create the event log if it doesn't exist yet.
        /// </summary>
        private void CreateEventLog() {
            try {
                if (!EventLog.SourceExists(ServiceName)) {
                    EventLog.CreateEventSource(
                        ServiceName + "Log", ServiceName);
                }
                eventLog.Source = ServiceName + "Log";
                eventLog.Log = ServiceName;
            }
            catch (Exception ex) {
                Log.Error("Error in createEventLog(): " + ex.Message, true);
            }
        }

        /// <summary>
        /// Load the initial settings.
        /// </summary>
        private void LoadSettings() {
            try {
                LoadConfig();
                LoadStatus();
            }
            catch (Exception ex) {
                Log.Error("LoadSettings() failed: {0}\n{1}", ex.Message, ex.StackTrace);
                throw new Exception("Error in LoadSettings.");
            }
        }

        /// <summary>
        /// Load the configuration and update the logger setups.
        /// </summary>
        private void LoadConfig() {
            try {
                _config = ServiceConfig.Deserialize(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "conf"));
            }
            catch (ConfigurationErrorsException ex) {
                Log.Error("Validating configuration failed: {0}", ex.Message);
                throw new Exception("Error validating configuration.");
            }
            catch (Exception ex) {
                Log.Error("Loading configuration failed: {0}", ex.Message);
                // this should terminate the service process:
                throw new Exception("Error loading configuration.");
            }
            // update the file logger with the configured log level:
            SetupFileLogging(_config.LogLevel);
            // the mail logger requires the configuration to be present, so we can call it now:
            SetupMailLogging();
        }

        /// <summary>
        /// Load the status.
        /// </summary>
        private void LoadStatus() {
	        var statusPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
		        "var", "status.xml");
			try {
                Log.Debug("Trying to load status from [{0}]", statusPath);
                _status = ServiceStatus.Deserialize(statusPath, _config);
                Log.Debug("Loaded status from [{0}]", statusPath);
            }
            catch (Exception ex) {
                Log.Error("loading status XML from [{0}] failed: {1} {2}",
	                statusPath, ex.Message, ex.StackTrace);
                // this should terminate the service process:
                throw new Exception("Error loading status.");
            }

            // now check the clean-shutdown status and send a notification if it was not true,
            // then set it to false while the service is running until it is properly
            // shut down via the OnStop() method:
            if (_status.CleanShutdown == false) {
                Log.Error("WARNING: {0} was not shut down properly last time!\n\nThis could " +
                          "indicate the computer has crashed or was forcefully shut off.", ServiceName);
            }
            _status.CleanShutdown = false;
        }

        /// <summary>
        /// Set up the performance monitor objects (CPU, Disk I/O, ...).
        /// </summary>
        private void InitializePerformanceMonitors() {
            var lodctr_msg = "Occasionally the performance counters cache becomes corrupted " +
                             "and needs to be reset manually. To do this, run the following " +
                             "command from a shell with elevated privileges:\n\n  lodctr /r\n\n";

            try {
                _cpu = new Cpu {
                    Interval = 250,
                    Limit = _config.MaxCpuUsage,
                    Probation = 16,
                    LogPerformanceReadings = _config.MonitoringLogLevel,
                    Enabled = true
                };
                _cpu.LoadAboveLimit += OnLoadAboveLimit;
                _cpu.LoadBelowLimit += OnLoadBelowLimit;
            }
            catch (UnauthorizedAccessException ex) {
                Log.Error("Not enough permissions to monitor the CPU load.\nMake sure the " +
                          "service account is a member of the [Performance Monitor Users] " +
                          "system group.\nError message was: {0}", ex.Message);
                throw;
            }
            catch (Exception ex) {
                Log.Error("Unexpected error initializing CPU monitoring: {0}", ex.Message);
                Log.Error(lodctr_msg);
                throw;
            }

            try {
                _phyDisk = new PhysicalDisk {
                    Interval = 250,
                    Limit = (float) _config.MaxDiskQueue / 1000,
                    Probation = 16,
                    LogPerformanceReadings = _config.MonitoringLogLevel,
                    Enabled = true
                };
                _phyDisk.LoadAboveLimit += OnLoadAboveLimit;
                _phyDisk.LoadBelowLimit += OnLoadBelowLimit;
            }
            catch (Exception ex) {
                Log.Error("Unexpected error initializing PhysicalDisk monitoring: {0}\n" +
                          "Please make sure the service account is a member of the local" +
                          "group [Performance Monitor Users]!", ex.Message);
                Log.Error(lodctr_msg);
                throw;
            }
        }

        /// <summary>
        /// Write a summary of loaded config + status to the log.
        /// </summary>
        private void StartupSummary() {
            var msg = "Startup Summary:\n\n";

            var thisAssembly = Assembly.GetExecutingAssembly();
            var thisVersionInfo = FileVersionInfo.GetVersionInfo(thisAssembly.Location);
            msg += "------ Assembly Information ------\n" +
                   $" > version: {thisAssembly.GetName().Version}\n" +
                   $" > file version: {thisVersionInfo.FileVersion}\n" +
                   $" > description: {thisVersionInfo.Comments}\n" +
                   $" > version information: {thisVersionInfo.ProductVersion}\n";

            var roboAssembly = Assembly.GetAssembly(typeof(RoboCommand));
            var roboVersionInfo = FileVersionInfo.GetVersionInfo(roboAssembly.Location);
            msg += "\n------ RoboSharp ------\n" +
                   $" > location: [{roboAssembly.Location}]\n" +
                   $" > version: {roboAssembly.GetName().Version}\n" +
                   $" > file version: {roboVersionInfo.FileVersion}\n" +
                   $" > description: {roboVersionInfo.Comments}\n" +
                   $" > version information: {roboVersionInfo.ProductVersion}\n";

            _versionSummary = $"AutoTx {Properties.Resources.BuildCommit.Trim()} " +
                              $"{Properties.Resources.BuildDate.Trim()} | " +
                              $"RoboSharp {roboAssembly.GetName().Version} " +
                              $"{roboVersionInfo.ProductVersion}";


            msg += "\n------ Loaded status flags ------\n" + _status.Summary() +
                   "\n------ Loaded configuration settings ------\n" + _config.Summary();


            var health = SystemChecks.HealthReport(_storage, _config.HostAlias);
            SendHealthReport(health);
            msg += "\n" + health;

            Log.Debug(msg);
            
            // finally check if the validation gave warnings and send them to the admin:
            var warnings = ServiceConfig.ValidatorWarnings;
            if (string.IsNullOrWhiteSpace(warnings))
                return;
            SendAdminEmail(warnings);
        }

        #endregion

        #region overrides for ServiceBase methods (start, stop, ...)

        /// <summary>
        /// Is executed when the service starts
        /// </summary>
        protected override void OnStart(string[] args) {
            try {
                _mainTimer = new Timer(_config.ServiceTimer);
                _mainTimer.Elapsed += OnTimedEvent;
                _mainTimer.Enabled = true;
            }
            catch (Exception ex) {
                Log.Error("Error in OnStart(): {0}", ex.Message);
                throw;
            }

            // read the build timestamp from the resources:
            var buildTimestamp = Properties.Resources.BuildDate.Trim();
            var buildCommitName = Properties.Resources.BuildCommit.Trim();
            var assembly = Assembly.GetExecutingAssembly();
            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var roboAssembly = Assembly.GetAssembly(typeof(RoboCommand));
            var roboVersionInfo = FileVersionInfo.GetVersionInfo(roboAssembly.Location);


            Log.Info("Email version string: [{0}]", _versionSummary);
            Log.Info("=".PadLeft(80, '='));
            Log.Info("{0} service started.", ServiceName);
            Log.Info("build:  [{0}]", buildTimestamp);
            Log.Info("commit: [{0}]", buildCommitName);
            Log.Info("version: [{0}]", assembly.GetName().Version);
            Log.Info("product version: [{0}]", versionInfo.ProductVersion);
            Log.Info("-".PadLeft(80, '-'));
            Log.Info("RoboSharp version: [{0}]", roboAssembly.GetName().Version);
            Log.Info("Robosharp product version: [{0}]", roboVersionInfo.ProductVersion);
            Log.Info("=".PadLeft(80, '='));
            Log.Trace($"ServiceBase.CanShutdown property: {CanShutdown}");
            Log.Trace("=".PadLeft(80, '='));
        }

        /// <summary>
        /// Executes when a Stop command is sent to the service by the Service Control Manager.
        /// NOTE: the method is NOT triggered when the operating system shuts down, instead
        /// the OnShutdown() method is used!
        /// </summary>
        protected override void OnStop() {
            Log.Warn("{0} service stop requested...", ServiceName);
            if (_transferState != TxState.Stopped) {
                _transferState = TxState.DoNothing;
                // RoboCommand.Stop() is calling Process.Kill() (immediately forcing a termination
                // of the process, returning asynchronously), followed by Process.Dispose()
                // (releasing all resources used by the component). Would be nice if RoboSharp
                // implemented a method to check if the process has actually terminated, but
                // this is probably something we have to do ourselves.
                // TODO: this has probably improved with recent versions of RoboSharp, check it!
                try {
                    _roboCommand.Stop();
                }
                catch (Exception ex) {
                    Log.Error("Error terminating the RoboCopy process: {0}", ex.Message);
                }
                _status.TransferInProgress = true;
                Log.Info("Not all files were transferred - will resume upon next start");
                Log.Debug("CurrentTransferSrc: " + _status.CurrentTransferSrc);
                // should we delete an incompletely transferred file on the target?
                SendTransferInterruptedMail();
            }
            // set the shutdown status to clean:
            _status.CleanShutdown = true;
            Log.Info("-----------------------");
            Log.Info("{0} service stopped", ServiceName);
            Log.Info("-----------------------");
        }

        /// <summary>
        /// Executes when the operating system is shutting down. Unlike some documentation says
        /// it doesn't call the OnStop() method, so we have to do this explicitly.
        /// </summary>
        protected override void OnShutdown() {
            Log.Warn("System is shutting down, requesting the service to stop.");
            OnStop();
        }

        /// <summary>
        /// Is executed when the service continues
        /// </summary>
        protected override void OnContinue() {
            Log.Info("-------------------------");
            Log.Info("{0} service resuming", ServiceName);
            Log.Info("-------------------------");
        }

        /// <summary>
        /// Timer Event
        /// </summary>
        private void OnTimedEvent(object source, ElapsedEventArgs e) {
            if (_transferState == TxState.DoNothing) return;

            // first disable the timer event to prevent another one from being triggered
            // while this method has not finished yet:
            _mainTimer.Enabled = false;

            try {
                RunMainTasks();
                GC.Collect();
                // if everything went fine, reset the timer interval to its default value, so the
                // service can even recover from temporary problems itself (see below):
                _mainTimer.Interval = _config.ServiceTimer;
            }
            catch (Exception ex) {
                // in case an Exception is reaching this level there is a good chance it is a
                // permanent / recurring problem, therefore we increase the timer interval each
                // time by a factor of ten (to avoid triggering the same issue every second and
                // flooding the admins with emails):
                _mainTimer.Interval *= 10;
                // make sure the interval doesn't exceed half a day:
                const int maxInterval = 1000 * 60 * 60 * 12;
                if (_mainTimer.Interval > maxInterval)
                    _mainTimer.Interval = maxInterval;
                Log.Error("Unhandled exception in OnTimedEvent(): {0}\n\n" +
                    "Trying exponential backoff, setting timer interval to {1} ms ({3}).\n\n" +
                    "StackTrace: {2}", ex.Message, _mainTimer.Interval, ex.StackTrace,
                    TimeUtils.SecondsToHuman((long)_mainTimer.Interval / 1000, false));
            }
            finally {
                // make sure to enable the timer again:
                _mainTimer.Enabled = true;
            }
        }

        #endregion

        #region general methods

        /// <summary>
        /// Event handler for load dropping below the configured limit(s).
        /// </summary>
        private void OnLoadBelowLimit(object sender, EventArgs e) {
            _exceedingLoadLimit--;
            if (_exceedingLoadLimit < 0)
                _exceedingLoadLimit = 0;
            Log.Log(_config.MonitoringLogLevel,
                "Received 'low-load' from {0} (exceeding: {1})", sender, _exceedingLoadLimit);
            if (_exceedingLoadLimit == 0)
                ResumePausedTransfer();
        }

        /// <summary>
        /// Event handler for load exceeding the configured limits.
        /// </summary>
        private void OnLoadAboveLimit(object sender, EventArgs e) {
            _exceedingLoadLimit++;
            Log.Log(_config.MonitoringLogLevel,
                "Received 'high-load' from {0} (exceeding: {1})", sender, _exceedingLoadLimit);
            PauseTransfer();
        }

        /// <summary>
        /// Check system parameters for valid ranges and update the global service state accordingly.
        /// </summary>
        private void UpdateServiceState() {
            var suspendReasons = new List<string>();

            // check all system parameters for valid ranges and remember the reason in a string
            // if one of them is failing (to report in the log why we're suspended)
            if (_phyDisk.HighLoad)
                suspendReasons.Add("Disk I/O");

            if (_cpu.HighLoad)
                suspendReasons.Add("CPU");

            if (SystemChecks.GetFreeMemory() < _config.MinAvailableMemory)
                suspendReasons.Add("RAM");

            var blacklistedProcess = SystemChecks.CheckForBlacklistedProcesses(
                _config.BlacklistedProcesses);
            if (!string.IsNullOrWhiteSpace(blacklistedProcess)) {
                suspendReasons.Add("process '" + blacklistedProcess + "'");
            }
            
            // all parameters within valid ranges, so set the state to "Running":
            if (suspendReasons.Count == 0) {
                _status.SetSuspended(false, "all parameters in valid ranges");
                return;
            }
            
            // set state to "Running" if no-one is logged on:
            if (SystemChecks.NoUserIsLoggedOn()) {
                _status.SetSuspended(false, "no user is currently logged on");
                return;
            }

            // by reaching this point we know the service should be suspended:
            _status.SetSuspended(true, string.Join(", ", suspendReasons));
        }

        /// <summary>
        /// Do the main tasks of the service, check system state, trigger transfers, ...
        /// </summary>
        private void RunMainTasks() {
            // Log.Error("test the email rate limiting for error messages...");
            // throw new Exception("just a test exception from RunMainTasks");

            // mandatory tasks, run on every call:
            SendLowSpaceMail();
            UpdateServiceState();
            _status.SerializeHeartbeat();

            if (TimeUtils.SecondsSince(_lastUserDirCheck) >= 120)
                _lastUserDirCheck = FsUtils.CreateIncomingDirectories(
                    _config.DestinationDirectory, _config.TmpTransferDir, _config.IncomingPath);

            // tasks depending on the service state:
            if (_status.ServiceSuspended) {
                // make sure to pause any running transfer:
                PauseTransfer();
            } else {
                // always check the incoming dirs, independently of running transfers:
                CheckIncomingDirectories();
                // now trigger potential transfer tasks:
                RunTransferTasks();
            }
        }

        /// <summary>
        /// Handler for debug messages from the RoboSharp library.
        /// </summary>
        private static void HandleDebugMessage(object sender, Debugger.DebugMessageArgs e) {
            Log.Debug("[RoboSharp-Debug] {0}", e.Message);
        }

        #endregion

        #region transfer tasks

        /// <summary>
        /// Assemble the transfer destination path and check if it exists.
        /// </summary>
        /// <param name="dirName">The target directory to be checked on the destination.</param>
        /// <returns>The full path if it exists, an empty string otherwise.</returns>
        private string DestinationPath(string dirName) {
            var destPath = Path.Combine(_config.DestinationDirectory, dirName);
            if (Directory.Exists(destPath))
                return destPath;

            return "";
        }
        
        /// <summary>
        /// Check for transfers to be finished, resumed or newly initiated.
        /// </summary>
        private void RunTransferTasks() {
            // only proceed when in a valid state:
            if (_transferState != TxState.Stopped &&
                _transferState != TxState.Paused) 
                return;

            // if we're paused, resume the transfer and DO NOTHING ELSE:
            if (_transferState == TxState.Paused) {
                ResumePausedTransfer();
                return;
            }

            // first check if there are finished transfers and clean them up:
            FinalizeTransfers();

            // next check if there is a transfer that has to be resumed:
            ResumeInterruptedTransfer();

            // check the queueing location and dispatch new transfers:
            ProcessQueuedDirectories();

            // decrease the new-transfer-wait-counter:
            _waitCyclesBeforeNextTx--;
        }

        /// <summary>
        /// Process directories in the queueing location, dispatching new transfers if applicable.
        /// </summary>
        private void ProcessQueuedDirectories() {
            // only proceed when in a valid state:
            if (_transferState != TxState.Stopped)
                return;

            // check the "processing" location for directories:
            var queued = new DirectoryInfo(_config.ProcessingPath).GetDirectories();
            if (queued.Length == 0)
                return;

            var subdirs = queued[0].GetDirectories();
            // having no subdirectories should not happen in theory - in practice it could e.g. if
            // an admin is moving around stuff while the service is operating, so better be safe:
            if (subdirs.Length == 0) {
                Log.Warn("WARNING: empty processing directory found: {0}", queued[0].Name);
                try {
                    queued[0].Delete();
                    Log.Debug("Removed empty directory: {0}", queued[0].Name);
                }
                catch (Exception ex) {
                    Log.Error("Error deleting directory: {0} - {1}", queued[0].Name, ex.Message);
                }
                return;
            }

            // give the system some time before starting the next transfer:
            if (_waitCyclesBeforeNextTx > 0) {
                Log.Debug("Waiting {0} more cycles before starting the next transfer...",
                    _waitCyclesBeforeNextTx);
                return;
            }

            // dispatch the next directory from "processing" for transfer:
            try {
                StartTransfer(subdirs[0].FullName);
            }
            catch (Exception ex) {
                Log.Error("Error checking for data to be transferred: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Check the incoming directories for files, move them to the processing location.
        /// </summary>
        private void CheckIncomingDirectories() {
            // iterate over all user-subdirectories:
            foreach (var userDir in new DirectoryInfo(_config.IncomingPath).GetDirectories()) {
                if (FsUtils.DirEmptyExcept(userDir, _config.MarkerFile))
                    continue;

                if (_incomingIgnore.Contains(userDir.FullName)) {
                    Log.Trace("Ignoring directory in incoming location: [{0}]", userDir.FullName);
                    continue;
                }

                Log.Info("Found new files in [{0}]", userDir.FullName);
                if (MoveToManagedLocation(userDir))
                    continue;

                // if moving to the processing location failed, add it to the ignore list:
                Log.Error("=== Ignoring [{0}] for this service session ===", userDir.FullName);
                _incomingIgnore.Add(userDir.FullName);
            }
        }

        /// <summary>
        /// Check if a transfer needs to be completed by moving its data from the target temp dir
        /// to the final (user) destination and by locally moving the transferred folders to the
        /// grace location for deferred deletion.
        /// </summary>
        private void FinalizeTransfers() {
            // NOTE: this is intentionally triggered by the timer only to make sure the cleanup
            // only happens while all system parameters are within their valid ranges

            // make sure the service is in an expected state before cleaning up:
            if (_transferState != TxState.Stopped || _status.TransferInProgress)
                return;
            
            if (_status.TxTargetUser.Length > 0) {
                Log.Debug("Finalizing transfer, cleaning up target storage location...");
                var finalDst = DestinationPath(_status.TxTargetUser);
                if (!string.IsNullOrWhiteSpace(finalDst)) {
                    if (FsUtils.MoveAllSubDirs(new DirectoryInfo(_status.TxTargetTmp),
                        finalDst, _config.EnforceInheritedACLs)) {
                        _status.TxTargetUser = "";
                    }
                }
            }

            if (_status.CurrentTransferSrc.Length <= 0)
                return;

            if (_transferredFiles.Count == 0) {
                var msg = "FinalizeTransfers: CurrentTransferSrc is set to " +
                          $"[{_status.CurrentTransferSrc}], but the list of transferred " +
                          "files is empty!\nThis indicates something went wrong during the " +
                          "transfer, maybe a local permission problem?";
                Log.Warn(msg);
                SendAdminEmail(msg, "Error Finalizing Transfer!");
                try {
                    var preserve = _status.CurrentTransferSrc
                        .Replace(_config.ManagedPath, "")
                        .Replace(@"\", "___");
                    preserve = Path.Combine(_config.ErrorPath, preserve);
                    var stale = new DirectoryInfo(_status.CurrentTransferSrc);
                    stale.MoveTo(preserve);
                    Log.Warn("Moved stale transfer source to [{0}]!", preserve);
                }
                catch (Exception ex) {
                    Log.Error("Preserving the stale transfer source [{0}] in [{1}] failed: {2}",
                        _status.CurrentTransferSrc, _config.ErrorPath, ex.Message);
                }

                // reset current transfer variables:
                _status.CurrentTransferSrc = "";
                _status.CurrentTransferSize = 0;

                return;
            }
            Log.Debug("Finalizing transfer, moving local data to grace location...");
            MoveToGraceLocation();
            SendTransferCompletedMail();
            _status.CurrentTransferSrc = ""; // cleanup completed, so reset CurrentTransferSrc
            _status.CurrentTransferSize = 0;
            _transferredFiles.Clear(); // empty the list of transferred files
        }

        /// <summary>
        /// Check if an interrupted (service shutdown) transfer exists and whether the current
        /// state allows for resuming it.
        /// </summary>
        private void ResumeInterruptedTransfer() {
            // CONDITIONS (a transfer has to be resumed):
            // - TxTargetUser has to be non-empty
            // - TransferState has to be "Stopped"
            // - TransferInProgress must be true
            if (_status.TxTargetUser.Length <= 0 ||
                _transferState != TxState.Stopped ||
                _status.TransferInProgress == false)
                return;

            Log.Debug("Resuming interrupted transfer from [{0}] to [{1}]",
                _status.CurrentTransferSrc, _status.TxTargetTmp);
            StartTransfer(_status.CurrentTransferSrc);
        }

        #endregion

        #region filesystem tasks (check, move, ...)

        /// <summary>
        /// Check the incoming directory for files and directories, move them over
        /// to the "processing" location (a sub-directory of ManagedDirectory).
        /// </summary>
        /// CAUTION: this method is called as a consequence of the main timer being triggered, so
        /// be aware that any message dispatched here could potentially show up every second!
        /// <param name="userDir">The directory to be moved to the processing location.</param>
        /// <returns>True in case of success, false otherwise.</returns>
        private bool MoveToManagedLocation(DirectoryInfo userDir) {
            string errMsg;
            try {
                // first check for individual files and collect them:
                FsUtils.CollectOrphanedFiles(userDir, _config.MarkerFile);

                // the default path where folders will be picked up by the actual transfer method:
                var processingPath = _config.ProcessingPath;

                // if the user has no directory on the destination move to UNMATCHED instead:
                if (string.IsNullOrWhiteSpace(DestinationPath(userDir.Name))) {
                    Log.Error("Found unmatched incoming dir: {0}", userDir.Name);
                    processingPath = _config.UnmatchedPath;
                }
                
                // now everything that is supposed to be transferred is in a folder,
                // for example: D:\ATX\PROCESSING\2017-04-02__12-34-56\user00
                var timeStamp = TimeUtils.Timestamp();
                var targetDir = Path.Combine(processingPath, timeStamp, userDir.Name);
                if (FsUtils.MoveAllSubDirs(userDir, targetDir))
                    return true;

                errMsg = $"unable to move [{userDir.FullName}]";
                // just to be safe, don't delete the failed directory from the processing location
                // but move it to the error location (in case something has in fact been moved
                // there already we have to make sure not to kill it):
                var moveFromProcessing = new DirectoryInfo(Path.Combine(processingPath, timeStamp));
                try {
                    moveFromProcessing.MoveTo(Path.Combine(_config.ErrorPath, timeStamp));
                    Log.Debug("Moved failed directory [{0}] out of processing to [{1}].",
                        moveFromProcessing.FullName, _config.ErrorPath);
                }
                catch (Exception ex) {
                    Log.Error("Moving [{0}] to [{1}] failed: {2}",
                        moveFromProcessing.FullName, _config.ErrorPath, ex.Message);
                    throw;
                }
            }
            catch (Exception ex) {
                errMsg = ex.Message;
            }
            Log.Error("=== Moving directory [{0}] to the processing location failed: {1} ===",
                userDir.FullName, errMsg);
            return false;
        }

        /// <summary>
        /// Move transferred files to the grace location for deferred deletion. Data is placed in
        /// a subdirectory with the current date and time as its name to denote the timepoint
        /// when the grace period for this data starts.
        /// </summary>
        private void MoveToGraceLocation() {
            string errMsg;
            // CurrentTransferSrc is e.g. D:\ATX\PROCESSING\2017-04-02__12-34-56\user00
            var sourceDirectory = new DirectoryInfo(_status.CurrentTransferSrc);
            var dstPath = Path.Combine(
                _config.DonePath,
                sourceDirectory.Name, // the username directory
                TimeUtils.Timestamp());
            Log.Trace("MoveToGraceLocation: src({0}) dst({1})", sourceDirectory.FullName, dstPath);

            try {
                if (FsUtils.MoveAllSubDirs(sourceDirectory, dstPath)) {
                    // clean up the processing location:
                    sourceDirectory.Delete();
                    if (sourceDirectory.Parent != null)
                        sourceDirectory.Parent.Delete();
                    // check grace location and trigger a notification if necessary:
                    SendGraceLocationSummary();
                    return;
                }
                errMsg = "unable to move " + sourceDirectory.FullName;
            }
            catch (Exception ex) {
                errMsg = ex.Message;
            }
            Log.Error("MoveToGraceLocation() failed: {0}", errMsg);
        }

        /// <summary>
        /// Wrapper to check or create the spooling directories and the incoming dirs for all users.
        /// </summary>
        private void InitializeDirectories() {
            if (FsUtils.CheckSpoolingDirectories(_config.IncomingPath, _config.ManagedPath) == false) {
                const string msg = "ERROR checking spooling directories (incoming / managed)!";
                Log.Error(msg);
                throw new Exception(msg);
            }

            _lastUserDirCheck = FsUtils.CreateIncomingDirectories(
                _config.DestinationDirectory, _config.TmpTransferDir, _config.IncomingPath);
        }

        /// <summary>
        /// Set up the StorageStatus object using the current configuration.
        /// </summary>
        private void SetupStorageStatus() {
            _storage = new StorageStatus(_config);
        }
        
        #endregion

    }
}

