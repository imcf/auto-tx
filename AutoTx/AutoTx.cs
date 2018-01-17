using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.IO;
using System.Timers;
using System.DirectoryServices.AccountManagement;
using System.Globalization;
using System.Management;
using NLog;
using NLog.Config;
using NLog.Targets;
using ATXCommon;
using ATXCommon.Serializables;
using RoboSharp;

namespace AutoTx
{
    public partial class AutoTx : ServiceBase
    {
        #region global variables

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // naming convention: variables ending with "Path" are strings, variables
        // ending with "Dir" are DirectoryInfo objects
        private string _configPath;
        private string _statusPath;
        private string _incomingPath;
        private string _managedPath;

        private string[] _remoteUserDirs;
        private string[] _localUserDirs;

        private List<string> _transferredFiles = new List<string>();

        private int _txProgress;

        private const int MegaBytes = 1024 * 1024;
        private const int GigaBytes = 1024 * 1024 * 1024;

        private DateTime _lastUserDirCheck = DateTime.Now;

        // the transfer state:
        private enum TxState
        {
            Stopped = 0,
            // Stopped: the last transfer was finished successfully or none was started yet.
            // A new transfer may only be started if the service is in this state.

            Active = 1,
            // Active: a transfer is currently running (i.e. no new transfer may be started).

            Paused = 2,
            // Paused is assigned in PauseTransfer() which gets called from within RunMainTasks()
            // when system parameters are not in their valid range. It gets evaluated if the
            // parameters return to valid or if no user is logged on any more.

            DoNothing = 3
            // DoNothing is assigned when the service gets shut down (in the OnStop() method)
            // to prevent accidentially launching new transfers etc.
        }

        private TxState _transferState;

        private ServiceConfig _config;
        private ServiceStatus _status;

        private static Timer _mainTimer;

        #endregion

        #region initialize, load and check configuration + status

        public AutoTx() {
            InitializeComponent();
            SetupFileLogging();
            CreateEventLog();
            LoadSettings();
            CreateIncomingDirectories();
        }

        /// <summary>
        /// Set up NLog logging: targets, rules...
        /// </summary>
        private void SetupFileLogging() {
            var logConfig = new LoggingConfiguration();
            var fileTarget = new FileTarget {
                FileName = ServiceName + ".log",
                ArchiveAboveSize = 1000000,
                ArchiveFileName = ServiceName + ".{#}.log",
                MaxArchiveFiles = 9,
                KeepFileOpen = true,
                Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] ${message}",
                // Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] (${logger}) ${message}"
            };
            logConfig.AddTarget("file", fileTarget);
            var logRuleFile = new LoggingRule("*", LogLevel.Debug, fileTarget);
            logConfig.LoggingRules.Add(logRuleFile);
            LogManager.Configuration = logConfig;
        }

        /// <summary>
        /// Configure logging to email targets.
        /// 
        /// Depending on the configuration, set up the logging via email. If no SmtpHost or no
        /// AdminEmailAdress is configured, nothing will be done. If they're set in the config file,
        /// a log target for messages with level "Fatal" will be configured. In addition, if the
        /// AdminDebugEmailAdress is set, another target for "Error" level messages is configured
        /// using this address as recipient.
        /// </summary>
        private void SetupMailLogging() {
            try {
                if (string.IsNullOrWhiteSpace(_config.SmtpHost) ||
                    string.IsNullOrWhiteSpace(_config.AdminEmailAdress))
                    return;

                var logConfig = LogManager.Configuration;
                var mailTargetFatal = new MailTarget {
                    SmtpServer = _config.SmtpHost,
                    SmtpPort = _config.SmtpPort,
                    To = _config.AdminEmailAdress,
                    Name = "mailfatal",
                };
                logConfig.AddTarget(mailTargetFatal);
                logConfig.AddRuleForOneLevel(LogLevel.Fatal, mailTargetFatal);

                if (!string.IsNullOrWhiteSpace(_config.AdminDebugEmailAdress)) {
                    var mailTargetError = new MailTarget {
                        SmtpServer = _config.SmtpHost,
                        SmtpPort = _config.SmtpPort,
                        To = _config.AdminDebugEmailAdress,
                        Name = "mailerror",
                    };
                    logConfig.AddTarget(mailTargetError);
                    logConfig.AddRuleForOneLevel(LogLevel.Error, mailTargetError);
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
                _transferState = TxState.Stopped;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _configPath = Path.Combine(baseDir, "configuration.xml");
                _statusPath = Path.Combine(baseDir, "status.xml");

                LoadConfigXml();
                LoadStatusXml();

                _roboCommand = new RoboCommand();
            }
            catch (Exception ex) {
                // FIXME: combine log and admin-email!
                var msg = string.Format("Error in LoadSettings(): {0}\n{1}", ex.Message, ex.StackTrace);
                Log.Error(msg);
                SendAdminEmail(msg);
                throw new Exception("Error in LoadSettings.");
            }
            // NOTE: this is explicitly called *outside* the try-catch block so an Exception
            // thrown by the checker will not be silenced but cause the startup to fail:
            CheckConfiguration();
        }


        /// <summary>
        /// Load the configuration xml file.
        /// </summary>
        private void LoadConfigXml() {
            try {
                _config = ServiceConfig.Deserialize(_configPath);
                _incomingPath = Path.Combine(_config.SourceDrive, _config.IncomingDirectory);
                _managedPath = Path.Combine(_config.SourceDrive, _config.ManagedDirectory);
                Log.Debug("Loaded config from [{0}]", _configPath);
            }
            catch (ConfigurationErrorsException ex) {
                Log.Error("ERROR validating configuration file [{0}]: {1}",
                    _configPath, ex.Message);
                throw new Exception("Error validating configuration.");
            }
            catch (Exception ex) {
                // FIXME: combine log and admin-email!
                var msg = string.Format("Error loading configuration XML: {0}", ex.Message);
                Log.Error(msg);
                SendAdminEmail(msg);
                // this should terminate the service process:
                throw new Exception("Error loading config.");
            }
        }


        /// <summary>
        /// Load the status xml file.
        /// </summary>
        private void LoadStatusXml() {
            try {
                Log.Debug("Trying to load status from [{0}]", _statusPath);
                _status = ServiceStatus.Deserialize(_statusPath, _config);
                Log.Debug("Loaded status from [{0}]", _statusPath);
            }
            catch (Exception ex) {
                // FIXME: combine log and admin-email!
                var msg = string.Format("Error loading status XML from [{0}]: {1} {2}",
                    _statusPath, ex.Message, ex.StackTrace);
                Log.Error(msg);
                SendAdminEmail(msg);
                // this should terminate the service process:
                throw new Exception("Error loading status.");
            }
        }


        /// <summary>
        /// Check if loaded configuration is valid, print a summary to the log.
        /// </summary>
        public void CheckConfiguration() {
            // non-critical / optional configuration parameters:
            if (!string.IsNullOrWhiteSpace(_config.SmtpHost))
                SetupMailLogging();

            var configInvalid = false;
            if (CheckSpoolingDirectories() == false) {
                Log.Error("ERROR checking spooling directories (incoming / managed)!");
                configInvalid = true;
            }

            // terminate the service process if necessary:
            if (configInvalid) throw new Exception("Invalid config, check log file!");


            // check the clean-shutdown status and send a notification if it was not true,
            // then set it to false while the service is running until it is properly
            // shut down via the OnStop() method:
            if (_status.CleanShutdown == false) {
                // FIXME: combine log and admin-email!
                var msg = string.Format("WARNING: {0} was not shut down properly last time!\n\n" +
                         "This could indicate the computer has crashed or was forcefully shut off.",
                    ServiceName);
                Log.Warn(msg);
                SendAdminEmail(msg);
            }
            _status.CleanShutdown = false;

            StartupSummary();
        }

        /// <summary>
        /// Write a summary of loaded config + status to the log.
        /// </summary>
        private void StartupSummary() {
            var msg = "Startup Summary:\n\n------ RoboSharp ------\n";
            var roboDll = System.Reflection.Assembly.GetAssembly(typeof(RoboCommand)).Location;
            var versionInfo = FileVersionInfo.GetVersionInfo(roboDll);
            msg += " > DLL file: " + roboDll + "\n" +
                   " > DLL description: " + versionInfo.Comments + "\n" +
                   " > DLL version: " + versionInfo.FileVersion + "\n";


            msg += "\n------ Loaded status flags ------\n" + _status.Summary() +
                   "\n------ Loaded configuration settings ------\n" + _config.Summary();


            msg += "\n------ Current system parameters ------\n" +
                   "Hostname: " + Environment.MachineName + "\n" +
                   "Free system memory: " + GetFreeMemory() + " MB" + "\n";
            foreach (var driveToCheck in _config.SpaceMonitoring) {
                msg += "Free space on drive '" + driveToCheck.DriveName + "': " +
                       GetFreeDriveSpace(driveToCheck.DriveName) + "\n";
            }


            msg += "\n------ Grace location status (threshold: " + _config.GracePeriod + ") ------\n";
            try {
                var tmp = GraceLocationSummary(_config.GracePeriod);
                if (string.IsNullOrEmpty(tmp)) {
                    msg += " -- NO EXPIRED folders in grace location! --\n";
                } else {
                    msg += tmp;
                }
            }
            catch (Exception ex) {
                // FIXME: combine log and admin-email!
                var warn = string.Format("GraceLocationSummary() failed: {0}", ex.Message);
                Log.Warn(warn);
                SendAdminEmail(warn);
            }

            if (!string.IsNullOrEmpty(_config.ValidationWarnings)) {
                Log.Warn("WARNING: some configuration settings might not be optimal:\n{0}",
                    _config.ValidationWarnings);
            }
            if (!string.IsNullOrEmpty(_status.ValidationWarnings)) {
                Log.Warn("WARNING: some status parameters were invalid and have been reset:\n{0}",
                    _status.ValidationWarnings);
            }
            Log.Debug(msg);
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
                // FIXME: combine log and admin-email!
                var msg = string.Format("Error in OnStart(): {0}", ex.Message);
                Log.Error(msg);
                SendAdminEmail(msg);
            }

            // read the build timestamp from the resources:
            var buildTimestamp = Properties.Resources.BuildDate.Trim();
            var buildCommitName = Properties.Resources.BuildCommit.Trim();
            Log.Info("-----------------------");
            Log.Info("{0} service started.", ServiceName);
            Log.Info("build:  [{0}]", buildTimestamp);
            Log.Info("commit: [{0}]", buildCommitName);
            Log.Info("-----------------------");
        }

        /// <summary>
        /// Executes when a Stop command is sent to the service by the Service Control Manager.
        /// NOTE: the method is NOT triggered when the operating system shuts down, instead
        /// the OnShutdown() method is used!
        /// </summary>
        protected override void OnStop() {
            Log.Warn(ServiceName + " service stop requested...");
            if (_transferState != TxState.Stopped) {
                _transferState = TxState.DoNothing;
                // Stop() is calling Process.Kill() (immediately forcing a termination of the
                // process, returning asynchronously), followed by Process.Dispose()
                // (releasing all resources used by the component). Would be nice if RoboSharp
                // implemented a method to check if the process has actually terminated, but
                // this is probably something we have to do ourselves.
                try {
                    _roboCommand.Stop();
                }
                catch (Exception ex) {
                    // FIXME: combine log and admin-email!
                    var msg = string.Format("Error terminating the RoboCopy process: {0}", ex.Message);
                    Log.Error(msg);
                    SendAdminEmail(msg);
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
            }
            catch (Exception ex) {
                // TODO / FIXME: combine log and admin-email!
                var msg = string.Format("Error in OnTimedEvent(): {0}", ex.Message);
                Log.Error(msg);
                SendAdminEmail(msg);
                Log.Debug("Extended Error Info (StackTrace): {0}", ex.StackTrace);
            }
            finally {
                // make sure to enable the timer again:
                _mainTimer.Enabled = true;
            }
        }

        #endregion

        #region general methods

        /// <summary>
        /// Check system parameters for valid ranges and update the global service state accordingly.
        /// </summary>
        private void UpdateServiceState() {
            var limitReason = "";

            // check all system parameters for valid ranges and remember the reason in a string
            // if one of them is failing (to report in the log why we're suspended)
            if (GetCpuUsage() >= _config.MaxCpuUsage)
                limitReason = "CPU usage";
            else if (GetFreeMemory() < _config.MinAvailableMemory)
                limitReason = "RAM usage";
            else {
                var blacklistedProcess = CheckForBlacklistedProcesses();
                if (blacklistedProcess != "") {
                    limitReason = "blacklisted process '" + blacklistedProcess + "'";
                }
            }
            
            // all parameters within valid ranges, so set the state to "Running":
            if (string.IsNullOrEmpty(limitReason)) {
                _status.ServiceSuspended = false;
                if (!string.IsNullOrEmpty(_status.LimitReason)) {
                    _status.LimitReason = ""; // reset to force a message on next service suspend
                    Log.Info("Service resuming operation (all parameters in valid ranges).");
                }
                return;
            }
            
            // set state to "Running" if no-one is logged on:
            if (NoUserIsLoggedOn()) {
                _status.ServiceSuspended = false;
                if (!string.IsNullOrEmpty(_status.LimitReason)) {
                    _status.LimitReason = ""; // reset to force a message on next service suspend
                    Log.Info("Service resuming operation (no user logged on).");
                }
                return;
            }

            // by reaching this point we know the service should be suspended:
            _status.ServiceSuspended = true;
            if (limitReason == _status.LimitReason)
                return;
            Log.Info("Service suspended due to limitiations [{0}].", limitReason);
            _status.LimitReason = limitReason;
        }

        /// <summary>
        /// Do the main tasks of the service, check system state, trigger transfers, ...
        /// </summary>
        public void RunMainTasks() {
            // mandatory tasks, run on every call:
            CheckFreeDiskSpace();
            UpdateServiceState();

            var delta = (int)(DateTime.Now - _lastUserDirCheck).TotalSeconds;
            if (delta >= 120)
                CreateIncomingDirectories();

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

        #endregion

        #region ActiveDirectory, email address, user name, ...

        /// <summary>
        /// Check if a user is currently logged into Windows.
        /// 
        /// WARNING: this DOES NOT ACCOUNT for users logged in via RDP!!
        /// </summary>
        /// See https://stackoverflow.com/questions/5218778/ for the RDP problem.
        private bool NoUserIsLoggedOn() {
            var username = "";
            try {
                var searcher = new ManagementObjectSearcher("SELECT UserName " +
                                                            "FROM Win32_ComputerSystem");
                var collection = searcher.Get();
                username = (string) collection.Cast<ManagementBaseObject>().First()["UserName"];
            }
            catch (Exception ex) {
                // TODO / FIXME: combine log and admin-email!
                var msg = string.Format("Error in getCurrentUsername(): {0}", ex.Message);
                Log.Error(msg);
                SendAdminEmail(msg);
            }
            return username == "";
        }

        /// <summary>
        /// Get the user email address from ActiveDirectory.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>Email address of AD user, an empty string if not found.</returns>
        public string GetEmailAddress(string username) {
            try {
                using (var pctx = new PrincipalContext(ContextType.Domain)) {
                    using (var up = UserPrincipal.FindByIdentity(pctx, username)) {
                        if (up != null && !String.IsNullOrEmpty(up.EmailAddress)) {
                            return up.EmailAddress;
                        }
                    }
                }
            }
            catch (Exception ex) {
                Log.Warn("Can't find email address for {0}: {1}", username, ex.Message);
            }
            return "";
        }

        /// <summary>
        /// Get the full user name (human-friendly) from ActiveDirectory.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>A human-friendly string representation of the user principal.</returns>
        public string GetFullUserName(string username) {
            try {
                using (var pctx = new PrincipalContext(ContextType.Domain)) {
                    using (var up = UserPrincipal.FindByIdentity(pctx, username)) {
                        if (up != null) return up.GivenName + " " + up.Surname;
                    }
                }
            }
            catch (Exception ex) {
                Log.Warn("Can't find full name for {0}: {1}", username, ex.Message);
            }
            return "";
        }

        #endregion

        #region transfer tasks

        /// <summary>
        /// Helper method to generate the full path of the current temp directory.
        /// </summary>
        /// <returns>A string with the path to the last tmp dir.</returns>
        private string ExpandCurrentTargetTmp() {
            return Path.Combine(_config.DestinationDirectory,
                _config.TmpTransferDir,
                _status.CurrentTargetTmp);
        }

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
        public void RunTransferTasks() {
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
        }

        /// <summary>
        /// Process directories in the queueing location, dispatching new transfers if applicable.
        /// </summary>
        private void ProcessQueuedDirectories() {
            // only proceed when in a valid state:
            if (_transferState != TxState.Stopped)
                return;

            // check the "processing" location for directories:
            var processingDir = Path.Combine(_managedPath, "PROCESSING");
            var queued = new DirectoryInfo(processingDir).GetDirectories();
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
            foreach (var userDir in new DirectoryInfo(_incomingPath).GetDirectories()) {
                if (IncomingDirIsEmpty(userDir))
                    continue;

                Log.Info("Found new files in [{0}]", userDir.FullName);
                MoveToManagedLocation(userDir);
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
            
            if (_status.CurrentTargetTmp.Length > 0) {
                Log.Debug("Finalizing transfer, cleaning up target storage location...");
                var finalDst = DestinationPath(_status.CurrentTargetTmp);
                if (!string.IsNullOrWhiteSpace(finalDst)) {
                    if (MoveAllSubDirs(new DirectoryInfo(ExpandCurrentTargetTmp()), finalDst, true)) {
                        _status.CurrentTargetTmp = "";
                    }
                }
            }

            if (_status.CurrentTransferSrc.Length > 0) {
                Log.Debug("Finalizing transfer, moving local data to grace location...");
                MoveToGraceLocation();
                SendTransferCompletedMail();
                _status.CurrentTransferSrc = ""; // cleanup completed, so reset CurrentTransferSrc
                _status.CurrentTransferSize = 0;
                _transferredFiles.Clear(); // empty the list of transferred files
            }
        }

        /// <summary>
        /// Check if an interrupted (service shutdown) transfer exists and whether the current
        /// state allows for resuming it.
        /// </summary>
        private void ResumeInterruptedTransfer() {
            // CONDITIONS (a transfer has to be resumed):
            // - CurrentTargetTmp has to be non-empty
            // - TransferState has to be "Stopped"
            // - TransferInProgress must be true
            if (_status.CurrentTargetTmp.Length <= 0 ||
                _transferState != TxState.Stopped ||
                _status.TransferInProgress == false)
                return;

            Log.Debug("Resuming interrupted transfer from [{0}] to [{1}]",
                _status.CurrentTransferSrc, ExpandCurrentTargetTmp());
            StartTransfer(_status.CurrentTransferSrc);
        }

        #endregion

        #region filesystem tasks (check, move, ...)

        /// <summary>
        /// Check if a given directory is empty. If a marker file is set in the config a
        /// file with this name will be created inside the given directory and will be
        /// skipped itself when checking for files and directories.
        /// </summary>
        /// <param name="dirInfo">The directory to check.</param>
        /// <returns>True if access is denied or the dir is empty, false otherwise.</returns>
        private bool IncomingDirIsEmpty(DirectoryInfo dirInfo) {
            try {
                var filesInTree = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                if (string.IsNullOrEmpty(_config.MarkerFile))
                    return filesInTree.Length == 0;

                // check if there is ONLY the marker file:
                if (filesInTree.Length == 1 &&
                    filesInTree[0].Name.Equals(_config.MarkerFile))
                    return true;

                // make sure the marker file is there:
                var markerFilePath = Path.Combine(dirInfo.FullName, _config.MarkerFile);
                if (! File.Exists(markerFilePath))
                    File.Create(markerFilePath);

                return filesInTree.Length == 0;
            }
            catch (Exception e) {
                Log.Error("Error accessing directories: {0}", e.Message);
            }
            // if nothing triggered before, we pretend the dir is empty:
            return true;
        }

        /// <summary>
        /// Collect individual files in a user dir in a specific sub-directory. If a marker
        /// file is set in the configuration, this will be skipped in the checks.
        /// </summary>
        /// <param name="userDir">The user directory to check for individual files.</param>
        private void CollectOrphanedFiles(DirectoryInfo userDir) {
            var fileList = userDir.GetFiles();
            var orphanedDir = Path.Combine(userDir.FullName, "orphaned");
            try {
                if (fileList.Length > 1 ||
                    (string.IsNullOrEmpty(_config.MarkerFile) && fileList.Length > 0)) {
                    if (Directory.Exists(orphanedDir)) {
                        Log.Info("Orphaned directory already exists, skipping individual files.");
                        return;
                    }
                    Log.Debug("Found individual files, collecting them in 'orphaned' folder.");
                    CreateNewDirectory(orphanedDir, false);
                }
                foreach (var file in fileList) {
                    if (file.Name.Equals(_config.MarkerFile))
                        continue;
                    Log.Debug("Collecting orphan: [{0}]", file.Name);
                    file.MoveTo(Path.Combine(orphanedDir, file.Name));
                }
            }
            catch (Exception ex) {
                Log.Error("Error collecting orphaned files: {0}\n{1}", ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        /// Check the incoming directory for files and directories, move them over
        /// to the "processing" location (a sub-directory of ManagedDirectory).
        /// </summary>
        private void MoveToManagedLocation(DirectoryInfo userDir) {
            string errMsg;
            string logMsg;  // TODO / FIXME: cleanup var after fixing mail-logging
            try {
                // first check for individual files and collect them:
                CollectOrphanedFiles(userDir);

                // the default subdir inside the managed directory, where folders will be
                // picked up later by the actual transfer method:
                var target = "PROCESSING";

                // if the user has no directory on the destination move to UNMATCHED instead:
                if (string.IsNullOrWhiteSpace(DestinationPath(userDir.Name))) {
                    // TODO / FIXME: combine log and admin-email!
                    logMsg = string.Format("Found unmatched incoming dir: {0}", userDir.Name);
                    Log.Error(logMsg);
                    SendAdminEmail(logMsg);
                    target = "UNMATCHED";
                }
                
                // now everything that is supposed to be transferred is in a folder,
                // for example: D:\ATX\PROCESSING\2017-04-02__12-34-56\user00
                var targetDir = Path.Combine(
                    _managedPath,
                    target,
                    TimeUtils.Timestamp(),
                    userDir.Name);
                if (MoveAllSubDirs(userDir, targetDir))
                    return;
                errMsg = "unable to move " + userDir.FullName;
            }
            catch (Exception ex) {
                errMsg = ex.Message;
            }
            // TODO / FIXME: combine log and admin-email!
            logMsg = string.Format("MoveToManagedLocation({0}) failed: {1}", userDir.FullName, errMsg);
            Log.Error(logMsg);
            SendAdminEmail(logMsg);
        }

        /// <summary>
        /// Move transferred files to the grace location for deferred deletion. Data is placed in
        /// a subdirectory with the current date and time as its name to denote the timepoint
        /// when the grace period for this data starts.
        /// </summary>
        public void MoveToGraceLocation() {
            string errMsg;
            // CurrentTransferSrc is e.g. D:\ATX\PROCESSING\2017-04-02__12-34-56\user00
            var sourceDirectory = new DirectoryInfo(_status.CurrentTransferSrc);
            var dstPath = Path.Combine(
                _managedPath,
                "DONE",
                sourceDirectory.Name, // the username directory
                TimeUtils.Timestamp());
            // writeLogDebug("MoveToGraceLocation: src(" + sourceDirectory.FullName + ") dst(" + dstPath + ")");

            try {
                if (MoveAllSubDirs(sourceDirectory, dstPath)) {
                    // clean up the processing location:
                    sourceDirectory.Delete();
                    if (sourceDirectory.Parent != null)
                        sourceDirectory.Parent.Delete();
                    // check age and size of existing folders in the grace location after
                    // a transfer has completed, trigger a notification if necessary:
                    Log.Debug(GraceLocationSummary(_config.GracePeriod));
                    return;
                }
                errMsg = "unable to move " + sourceDirectory.FullName;
            }
            catch (Exception ex) {
                errMsg = ex.Message;
            }
            // TODO / FIXME: combine log and admin-email!
            var msg = string.Format("MoveToGraceLocation() failed: {0}", errMsg);
            Log.Error(msg);
            SendAdminEmail(msg);
        }

        /// <summary>
        /// Move all subdirectories of a given path into a destination directory. The destination
        /// will be created if it doesn't exist yet. If a subdirectory of the same name already
        /// exists in the destination, a timestamp-suffix is added to the new one.
        /// </summary>
        /// <param name="sourceDir">The source path as DirectoryInfo object.</param>
        /// <param name="destPath">The destination path as a string.</param>
        /// <param name="resetAcls">Whether to reset the ACLs on the moved subdirectories.</param>
        /// <returns>True on success, false otherwise.</returns>
        private bool MoveAllSubDirs(DirectoryInfo sourceDir, string destPath, bool resetAcls = false) {
            // TODO: check whether _transferState should be adjusted while moving dirs!
            Log.Debug("MoveAllSubDirs: [{0}] to [{1}]", sourceDir.FullName, destPath);
            try {
                // make sure the target directory that should hold all subdirectories to
                // be moved is existing:
                if (string.IsNullOrEmpty(CreateNewDirectory(destPath, false))) {
                    Log.Warn("WARNING: destination path doesn't exist: {0}", destPath);
                    return false;
                }

                foreach (var subDir in sourceDir.GetDirectories()) {
                    var target = Path.Combine(destPath, subDir.Name);
                    // make sure NOT to overwrite the subdirectories:
                    if (Directory.Exists(target))
                        target += "_" + TimeUtils.Timestamp();
                    Log.Debug(" - [{0}] > [{1}]", subDir.Name, target);
                    subDir.MoveTo(target);

                    if (resetAcls && _config.EnforceInheritedACLs) {
                        try {
                            var acl = Directory.GetAccessControl(target);
                            acl.SetAccessRuleProtection(false, false);
                            Directory.SetAccessControl(target, acl);
                            Log.Debug("Successfully reset inherited ACLs on [{0}]", target);
                        }
                        catch (Exception ex) {
                            Log.Error("Error resetting inherited ACLs on [{0}]:\n{1}",
                                target, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex) {
                // TODO / FIXME: combine log and admin-email!
                var msg = string.Format("Error moving directories: [{0}] > [{1}]\n{2}",
                    sourceDir.FullName, destPath, ex.Message);
                Log.Error(msg);
                SendAdminEmail(msg);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Create a directory with the given name if it doesn't exist yet, otherwise
        /// (optionally) create a new one using a date suffix to distinguish it from
        /// the existing one.
        /// </summary>
        /// <param name="dirPath">The full path of the directory to be created.</param>
        /// <param name="unique">Add a time-suffix to the name if the directory exists.</param>
        /// <returns>The name of the (created or pre-existing) directory. This will only
        /// differ from the input parameter "dirPath" if the "unique" parameter is set
        /// to true (then it will give the newly generated name) or if an error occured
        /// (in which case it will return an empty string).</returns>
        private string CreateNewDirectory(string dirPath, bool unique) {
            try {
                if (Directory.Exists(dirPath)) {
                    // if unique was not requested, return the name of the existing dir:
                    if (unique == false)
                        return dirPath;
                    
                    dirPath = dirPath + "_" + TimeUtils.Timestamp();
                }
                Directory.CreateDirectory(dirPath);
                Log.Debug("Created directory: [{0}]", dirPath);
                return dirPath;
            }
            catch (Exception ex) {
                // TODO / FIXME: combine log and admin-email!
                var msg = string.Format("Error in CreateNewDirectory({0}): {1}", dirPath, ex.Message);
                Log.Error(msg);
                SendAdminEmail(msg);
            }
            return "";
        }

        /// <summary>
        /// Helper method to check if a directory exists, trying to create it if not.
        /// </summary>
        /// <param name="path">The full path of the directory to check / create.</param>
        /// <returns>True if existing or creation was successful, false otherwise.</returns>
        private bool CheckForDirectory(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                Log.Error("ERROR: CheckForDirectory() parameter must not be empty!");
                return false;
            }
            return CreateNewDirectory(path, false) == path;
        }

        /// <summary>
        /// Ensure the required spooling directories (managed/incoming) exist.
        /// </summary>
        /// <returns>True if all dirs exist or were created successfully.</returns>
        private bool CheckSpoolingDirectories() {
            var retval = CheckForDirectory(_incomingPath);
            retval &= CheckForDirectory(_managedPath);
            retval &= CheckForDirectory(Path.Combine(_managedPath, "PROCESSING"));
            retval &= CheckForDirectory(Path.Combine(_managedPath, "DONE"));
            retval &= CheckForDirectory(Path.Combine(_managedPath, "UNMATCHED"));
            return retval;
        }

        /// <summary>
        /// Helper to create directories for all users that have one in the local
        /// user directory (C:\Users) AND in the DestinationDirectory.
        /// </summary>
        private void CreateIncomingDirectories() {
            _localUserDirs = new DirectoryInfo(@"C:\Users")
                .GetDirectories()
                .Select(d => d.Name)
                .ToArray();
            _remoteUserDirs = new DirectoryInfo(_config.DestinationDirectory)
                .GetDirectories()
                .Select(d => d.Name)
                .ToArray();

            foreach (var userDir in _localUserDirs) {
                // don't create an incoming directory for the same name as the
                // temporary transfer location:
                if (_config.TmpTransferDir == userDir)
                    continue;

                // don't create a directory if it doesn't exist on the target:
                if (!_remoteUserDirs.Contains(userDir))
                    continue;

                CreateNewDirectory(Path.Combine(_incomingPath, userDir), false);
            }
            _lastUserDirCheck = DateTime.Now;
        }

        /// <summary>
        /// Generate a report on expired folders in the grace location.
        /// 
        /// Check all user-directories in the grace location for subdirectories whose timestamp
        /// (the directory name) exceeds the configured grace period and generate a summary
        /// containing the age and size of those directories. The summary will be sent to the admin
        /// if the configured GraceNotificationDelta has passed since the last email.
        /// </summary>
        /// <param name="threshold">The number of days used as expiration threshold.</param>
        public string GraceLocationSummary(int threshold) {
            var expired = ExpiredDirs(threshold);
            var report = "";
            foreach (var userdir in expired.Keys) {
                report += "\n - user '" + userdir + "'\n";
                foreach (var subdir in expired[userdir]) {
                    report += string.Format("   - {0} [age: {2} days, size: {1} MB]\n",
                        subdir.Item1, subdir.Item2, subdir.Item3);
                }
            }
            if (string.IsNullOrEmpty(report))
                return "";
            report = "Expired folders in grace location:\n" + report;
            var delta = (int)(DateTime.Now - _status.LastGraceNotification).TotalMinutes;
            report += "\nTime since last grace notification: " + delta + "\n";
            if (delta >= _config.GraceNotificationDelta) {
                SendAdminEmail(report, "Grace location cleanup required.");
                _status.LastGraceNotification = DateTime.Now;
                report += "\nNotification sent to AdminEmailAdress.\n";
            }
            return report;
        }

        /// <summary>
        /// Assemble a dictionary with information about expired directories.
        /// </summary>
        /// <param name="thresh">The number of days used as expiration threshold.</param>
        /// <returns>A dictionary having usernames as keys (of those users that actually do have
        /// expired directories), where the values are lists of tuples with the DirInfo objects,
        /// size and age (in days) of the expired directories.</returns>
        private Dictionary<string, List<Tuple<DirectoryInfo, long, int>>> ExpiredDirs(int thresh) {
            var collection = new Dictionary<string, List<Tuple<DirectoryInfo, long, int>>>();
            var graceDir = new DirectoryInfo(Path.Combine(_managedPath, "DONE"));
            var now = DateTime.Now;
            foreach (var userdir in graceDir.GetDirectories()) {
                var expired = new List<Tuple<DirectoryInfo, long, int>>();
                foreach (var subdir in userdir.GetDirectories()) {
                    var age = DirNameToAge(subdir, now);
                    if (age < thresh)
                        continue;
                    long size = -1;
                    try {
                        size = FsUtils.GetDirectorySize(subdir.FullName) / MegaBytes;
                    }
                    catch (Exception ex) {
                        Log.Error("ERROR getting directory size of [{0}]: {1}",
                            subdir.FullName, ex.Message);
                    }
                    expired.Add(new Tuple<DirectoryInfo, long, int>(subdir, size, age));
                }
                if (expired.Count > 0)
                    collection.Add(userdir.Name, expired);
            }
            return collection;
        }

        /// <summary>
        /// Convert the timestamp given by the NAME of a directory into the age in days.
        /// </summary>
        /// <param name="dir">The DirectoryInfo object to check for its name-age.</param>
        /// <param name="baseTime">The DateTime object to compare to.</param>
        /// <returns>The age in days, or -1 in case of an error.</returns>
        private int DirNameToAge(DirectoryInfo dir, DateTime baseTime) {
            DateTime dirTimestamp;
            try {
                dirTimestamp = DateTime.ParseExact(dir.Name, "yyyy-MM-dd__HH-mm-ss",
                    CultureInfo.InvariantCulture);
            }
            catch (Exception ex) {
                Log.Warn("ERROR parsing timestamp from directory name [{0}], skipping: {1}",
                    dir.Name, ex.Message);
                return -1;
            }
            return (baseTime - dirTimestamp).Days;
        }

        #endregion

    }
}
