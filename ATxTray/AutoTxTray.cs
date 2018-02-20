using System;
using System.Diagnostics;
using System.Timers;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ATxCommon;
using ATxCommon.Serializables;
using Microsoft.WindowsAPICodePack.Dialogs;
using NLog;
using NLog.Config;
using NLog.Targets;
using Timer = System.Timers.Timer;

// ReSharper disable RedundantDefaultMemberInitializer

namespace ATxTray
{
    public class AutoTxTray : ApplicationContext
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // private static readonly string AppTitle = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
        private const string AppTitle = "AutoTx Tray Monitor";

        private static readonly Timer AppTimer = new Timer(1000);

        private static string _statusFile;
        private static string _submitPath;
        private static ServiceConfig _config;
        private static ServiceStatus _status;

        private static bool _statusChanged = false;
        private static bool _statusFileChanged = true;
        private static bool _serviceProcessAlive = false;
        private static bool _serviceSuspended = true;
        private static string _serviceSuspendReason;

        private static bool _txInProgress = false;
        private static long _txSize;
        private static int _txProgressPct;


        #region tray icon and context menu variables

        private readonly NotifyIcon _notifyIcon = new NotifyIcon();
        private readonly Icon _tiDefault = Properties.Resources.IconDefault;
        private readonly Icon _tiStopped = Properties.Resources.IconStopped;
        private readonly Icon _tiSuspended = Properties.Resources.IconSuspended;
        private readonly Icon _tiTx0 = Properties.Resources.IconTx0;
        private readonly Icon _tiTx1 = Properties.Resources.IconTx1;
        private readonly ContextMenuStrip _cmStrip = new ContextMenuStrip();
        private readonly ToolStripMenuItem _miExit = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTitle = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miSvcRunning = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miSvcSuspended = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTxProgress = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTxEnqueue = new ToolStripMenuItem();

        private readonly ToolStripProgressBar _miTxProgressBar = new ToolStripProgressBar();

        #endregion


        private static TaskDialog _confirmDialog;
        private static DirectoryInfo _selectedDir;

        /// <summary>
        /// Constructor setting up tray icon, config + status, timer and file system watcher.
        /// </summary>
        /// <param name="baseDir">The base directory of the AutoTx service installation.</param>
        public AutoTxTray(string baseDir) {

            SetupLogging();

            _statusFile = Path.Combine(baseDir, "status.xml");

            Log.Info("-----------------------");
            Log.Info("{0} initializing...", AppTitle);
            Log.Info("build: [{0}]", Properties.Resources.BuildDate.Trim());
            Log.Info("commit: [{0}]", Properties.Resources.BuildCommit.Trim());
            Log.Info("-----------------------");
            Log.Debug(" - status file: [{0}]", _statusFile);

            _notifyIcon.Icon = _tiStopped;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += PickDirectoryForNewTransfer;

            // this doesn't work properly, the menu will not close etc. so we disable it for now:
            // _notifyIcon.Click += ShowContextMenu;

            Log.Trace("Trying to read service config and status files...");
            try {
                _config = ServiceConfig.Deserialize(Path.Combine(baseDir, "conf"));
                UpdateStatusInformation();
                SetupContextMenu();
            }
            catch (Exception ex) {
                var msg = "Error during initialization: " + ex.Message;
                Log.Error(msg);
                _notifyIcon.ShowBalloonTip(5000, AppTitle, msg, ToolTipIcon.Error);
                // we cannot terminate the message loop (Application.Run()) while the constructor
                // is being run as it is not active yet - therefore we set the _status object to
                // null which will terminate the application during the next "Elapsed" event:
                _status = null;
                // suspend the thread for 5s to make sure the balloon tip is shown for a while:
                System.Threading.Thread.Sleep(5000);
            }

            _submitPath = Path.Combine(_config.IncomingPath, Environment.UserName);

            AppTimer.Elapsed += AppTimerElapsed;
            AppTimer.Enabled = true;
            Log.Trace("Enabled timer.");

            var fsw = new FileSystemWatcher {
                Path = baseDir,
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "status.xml",
            };
            fsw.Changed += StatusFileUpdated;
            fsw.EnableRaisingEvents = true;

            Log.Info("{0} initialization completed.", AppTitle);
        }

        /// <summary>
        /// Configure logging using a file target.
        /// </summary>
        private static void SetupLogging() {
            var logConfig = new LoggingConfiguration();
            var fileTarget = new FileTarget {
                FileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath) + ".log",
                Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] ${message}"
                // Layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss} [${level}] (${logger}) ${message}"
            };
            logConfig.AddTarget("file", fileTarget);
            var logRule = new LoggingRule("*", LogLevel.Debug, fileTarget);
            logConfig.LoggingRules.Add(logRule);
            LogManager.Configuration = logConfig;
        }

        /// <summary>
        /// Set up the tray icon context menu entries.
        /// </summary>
        private void SetupContextMenu() {
            Log.Trace("Building context menu...");
            _miExit.Text = @"Exit";
            _miExit.Click += AutoTxTrayExit;

            _miTitle.Font = new Font(_cmStrip.Font, FontStyle.Bold);
            _miTitle.Text = AppTitle;
            _miTitle.ToolTipText = Properties.Resources.BuildCommit.Trim();
            _miTitle.Image = _tiDefault.ToBitmap();
            _miTitle.BackColor = Color.LightCoral;
            _miTitle.Click += ShowContextMenu;

            _miSvcRunning.Text = @"Service NOT RUNNING!";
            _miSvcRunning.BackColor = Color.LightCoral;
            _miSvcRunning.Click += ShowContextMenu;

            _miSvcSuspended.Text = @"No limits apply, service active.";
            _miSvcSuspended.Click += ShowContextMenu;

            _miTxProgress.Text = @"No transfer running.";
            _miTxProgress.Click += ShowContextMenu;

            _miTxEnqueue.Text = @"+++ Add new directory for transfer. +++";
            _miTxEnqueue.Click += PickDirectoryForNewTransfer;

            _miTxProgressBar.ToolTipText = @"Current Transfer Progress";
            _miTxProgressBar.Value = 0;
            var size = _miTxProgressBar.Size;
            size.Width = 300;
            _miTxProgressBar.Size = size;

            _cmStrip.Items.AddRange(new ToolStripItem[] {
                _miTitle,
                _miSvcRunning,
                _miSvcSuspended,
                _miTxProgress,
                _miTxProgressBar,
                new ToolStripSeparator(),
                _miTxEnqueue,
                new ToolStripSeparator(),
                _miExit
            });

            _notifyIcon.ContextMenuStrip = _cmStrip;
            Log.Trace("Finished building context menu.");
        }

        /// <summary>
        /// Clean up the tray icon and shut down the application.
        /// </summary>
        private void AutoTxTrayExit() {
            _notifyIcon.Visible = false;
            Log.Info("Shutting down {0}.", AppTitle);
            Application.Exit();
        }

        /// <summary>
        /// Wrapper for AutoTxTrayExit to act as an event handler.
        /// </summary>
        private void AutoTxTrayExit(object sender, EventArgs e) {
            AutoTxTrayExit();
        }

        /// <summary>
        /// Update the tooltip making sure not to exceed the 63 characters limit.
        /// </summary>
        /// <param name="msg"></param>
        private void UpdateHoverText(string msg) {
            if (msg.Length > 63) {
                msg = msg.Substring(0, 60) + "...";
            }
            _notifyIcon.Text = msg;
        }

        /// <summary>
        /// Refresh status information and update tray icon and context menu items accordingly.
        /// </summary>
        private void AppTimerElapsed(object sender, ElapsedEventArgs e) {
            if (_status == null) {
                AutoTxTrayExit();
                return;
            }

            UpdateServiceProcessState();
            UpdateStatusInformation();  // update the status no matter if the service process is running

            var svcProcessRunning = "stopped";
            var statusHeartbeat = "?";
            var txProgress = "No";

            if (_serviceProcessAlive) {
                svcProcessRunning = "OK";
                if ((DateTime.Now - _status.LastStatusUpdate).TotalSeconds <= 60)
                    statusHeartbeat = "OK";
                if (_txInProgress)
                    txProgress = $"{_txProgressPct}%";
            }

            UpdateHoverText($"AutoTx [svc={svcProcessRunning}] [hb={statusHeartbeat}] [tx={txProgress}]");

            if (!_statusChanged)
                return;

            UpdateServiceSuspendedState();
            UpdateTxProgressBar();
            UpdateTxInProgressState();

            UpdateTrayIcon();
            _statusChanged = false;
        }

        /// <summary>
        /// Set global flag indicating the status file has changed and needs to be re-read.
        /// </summary>
        private static void StatusFileUpdated(object sender, FileSystemEventArgs e) {
            _statusFileChanged = true;
        }

        /// <summary>
        /// Event handler to make the context menu appear on the screen.
        /// </summary>
        private void ShowContextMenu(object sender, EventArgs e) {
            // just show the menu again, to avoid that clicking the menu item closes the context
            // menu without having to disable the item (which would grey out the text and icon):
            _notifyIcon.ContextMenuStrip.Show();
        }

        /// <summary>
        /// Let the user select a directory for starting a new transfer.
        /// </summary>
        private static void PickDirectoryForNewTransfer(object sender, EventArgs e) {
            if (!Directory.Exists(_submitPath)) {
                Log.Error("Current user has no incoming directory: [{0}]", _submitPath);
                MessageBox.Show($@"User '{Environment.UserName}' is not allowed to start transfers!",
                    @"User not registered for AutoTx", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var dirDialog = new CommonOpenFileDialog {
                Title = @"Select directory to be transferred",
                IsFolderPicker = true,
                EnsurePathExists = true,
                Multiselect = false,
                DefaultDirectory = _config.SourceDrive
            };

            if (dirDialog.ShowDialog() != CommonFileDialogResult.Ok)
                return;

            _selectedDir = new DirectoryInfo(dirDialog.FileName);
            var drive = dirDialog.FileName.Substring(0, 3);
            if (drive != _config.SourceDrive) {
                MessageBox.Show($@"The selected directory '{_selectedDir}' is required to be on " +
                    $@"drive {_config.SourceDrive}, please choose another directory!",
                    @"Selected directory on wrong drive", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            NewTxConfirmationDialog();
        }

        /// <summary>
        /// Let the user confirm the directory choice by presenting a summary with name, size etc.
        /// </summary>
        private static void NewTxConfirmationDialog() {
            var folderName = _selectedDir.Name;
            var locationPath = _selectedDir.Parent?.FullName;
            var size = Conv.BytesToString(FsUtils.GetDirectorySize(_selectedDir.FullName));

            const string caption = "AutoTx - Folder Selection Confirmation";
            const string instructionText = "Review your folder selection:";
            var footerText = "Selection summary:\n\n" +
                             $"Selected Folder:  [{folderName}]\n" +
                             $"Size:  {size}\n" +
                             $"Folder Location:  [{locationPath}]";

            _confirmDialog = new TaskDialog {
                Caption = caption,
                // Icon is buggy in the API and has to be set via an event handler, see below
                // Icon = TaskDialogStandardIcon.Shield,
                InstructionText = instructionText,
                FooterText = footerText,
                DetailsExpanded = true,
                StandardButtons = TaskDialogStandardButtons.Cancel,
            };
            // register the event handler to set the icon:
            _confirmDialog.Opened += TaskDialogOpened;

            var acceptBtn = new TaskDialogCommandLink("buttonAccept",
                $"Accept \"{folderName}\" with a total size of {size}.",
                $"Transfer \"{folderName}\" from \"{locationPath}\".");
            var changeBtn = new TaskDialogCommandLink("buttonCancel", "Select different folder...",
                "Do not use this folder, select another one instead.");
            acceptBtn.Click += ConfirmAcceptClick;
            changeBtn.Click += ConfirmChangeClick;

            _confirmDialog.Controls.Add(acceptBtn);
            _confirmDialog.Controls.Add(changeBtn);
            try {
                _confirmDialog.Show();
            }
            catch (Exception ex) {
                Log.Error("Showing the TaskDialog failed: {0}", ex.Message);
                var res = MessageBox.Show($@"{instructionText}\n{footerText}\n\n" +
                                @"Press [OK] to confirm selection.", caption,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (res == DialogResult.OK)
                    SubmitDirForNewTx();
            }
        }

        /// <summary>
        /// Dummy handler to set the TaskDialog icon.
        /// </summary>
        private static void TaskDialogOpened(object sender, EventArgs e) {
            var td = sender as TaskDialog;
            td.Icon = TaskDialogStandardIcon.Shield;
        }

        /// <summary>
        /// Close the confirmation dialog and submit the selected dir for transfer.
        /// </summary>
        private static void ConfirmAcceptClick(object sender, EventArgs e) {
            _confirmDialog.Close();
            SubmitDirForNewTx();
        }

        /// <summary>
        /// Close the confirmation dialog and re-show the directory picker.
        /// </summary>
        private static void ConfirmChangeClick(object sender, EventArgs e) {
            _confirmDialog.Close();
            Log.Debug("User wants to change directory choice.");
            PickDirectoryForNewTransfer(sender, e);
        }

        /// <summary>
        /// Submit the selected directory as a new transfer.
        /// 
        /// The chosen folder will be moved to the AutoTx "incoming" location of the current user
        /// where it will be picked up by the service as a new transfer.
        /// </summary>
        private static void SubmitDirForNewTx() {
            Log.Debug($"User accepted directory choice [{_selectedDir.FullName}].");
            var tgtPath = Path.Combine(_submitPath, _selectedDir.Name);
            try {
                Directory.Move(_selectedDir.FullName, tgtPath);
            }
            catch (Exception ex) {
                Log.Error("Moving [{0}] to [{1}] failed: {2}", _selectedDir.FullName, tgtPath, ex);
                MessageBox.Show($@"Error submitting {_selectedDir.FullName} for transfer: {ex}",
                    @"AutoTx New Transfer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Log.Info($"Submitted new transfer: [{_selectedDir.FullName}].");
        }

        /// <summary>
        /// Read (or re-read) the service status file if it has changed since last time.
        /// </summary>
        private static void UpdateStatusInformation() {
            if (!_statusFileChanged)
                return;

            Log.Trace("Status file was updated, trying to re-read...");
            _status = ServiceStatus.Deserialize(_statusFile, _config);
            _statusFileChanged = false;
            _statusChanged = true;
        }

        /// <summary>
        /// Check if a process with the expeced name of the service is currently running.
        /// </summary>
        /// <returns>True if such a process exists, false otherwise.</returns>
        private static bool IsServiceProcessAlive() {
            var plist = Process.GetProcessesByName("AutoTx");
            return plist.Length > 0;
        }

        /// <summary>
        /// Check if the service process is alive and update context menu entries accordingly.
        /// </summary>
        private void UpdateServiceProcessState() {
            var isServiceProcessAlive = IsServiceProcessAlive();
            if (_serviceProcessAlive == isServiceProcessAlive)
                return;

            _serviceProcessAlive = isServiceProcessAlive;
            if (_serviceProcessAlive) {
                _miSvcRunning.Text = @"Service running.";
                _miSvcRunning.BackColor = Color.LightGreen;
                _miTitle.BackColor = Color.LightGreen;
                _miSvcSuspended.Enabled = true;
                /*
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "Service running.", ToolTipIcon.Info);
                 */
            } else {
                _miSvcRunning.Text = @"Service NOT RUNNING!";
                _miSvcRunning.BackColor = Color.LightCoral;
                _miTitle.BackColor = Color.LightCoral;
                _miSvcSuspended.Enabled = false;
                /*
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "Service stopped.", ToolTipIcon.Error);
                 */
            }
        }

        /// <summary>
        /// Update the context menu with the current "suspended" state of the service.
        /// </summary>
        private void UpdateServiceSuspendedState() {
            // first update the suspend reason as this can possibly change even if the service
            // never leaves the suspended state and we should still display the correct reason:
            if (_serviceSuspendReason == _status.LimitReason &&
                _serviceSuspended == _status.ServiceSuspended)
                return;

            _serviceSuspended = _status.ServiceSuspended;
            _serviceSuspendReason = _status.LimitReason;
            if (_serviceSuspended) {
                _miSvcSuspended.Text = @"Service suspended, reason: " + _serviceSuspendReason;
                _miSvcSuspended.BackColor = Color.LightSalmon;
                /*
                _notifyIcon.ShowBalloonTip(500, "AutoTx Monitor",
                    "Service suspended: " + _status.LimitReason, ToolTipIcon.Warning);
                 */
            } else {
                _miSvcSuspended.Text = @"No limits apply, service active.";
                _miSvcSuspended.BackColor = Color.LightGreen;
                /*
                _notifyIcon.ShowBalloonTip(500, "AutoTx Monitor",
                    "Service resumed, no limits apply.", ToolTipIcon.Info);
                 */
            }
        }

        /// <summary>
        /// Update the context menu regarding the current transfer state, show a balloon tooltip
        /// if the transfer status has changed.
        /// </summary>
        private void UpdateTxInProgressState() {
            if (_txInProgress == _status.TransferInProgress &&
                _txSize == _status.CurrentTransferSize)
                return;

            _txInProgress = _status.TransferInProgress;
            _txSize = _status.CurrentTransferSize;
            if (_txInProgress) {
                _miTxProgress.Text = $@"Transfer in progress (size: {Conv.BytesToString(_txSize)})";
                _miTxProgress.BackColor = Color.LightGreen;
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "New transfer started (size: " +
                    Conv.BytesToString(_txSize) + ").", ToolTipIcon.Info);
            } else {
                _miTxProgress.Text = @"No transfer running.";
                _miTxProgress.ResetBackColor();
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "Transfer completed.", ToolTipIcon.Info);
            }
        }

        /// <summary>
        /// Update the transfer progress bar.
        /// </summary>
        private void UpdateTxProgressBar() {
            if (_txInProgress == _status.TransferInProgress &&
                _txProgressPct == _status.CurrentTransferPercent)
                return;

            _txProgressPct = _status.CurrentTransferPercent;
            if (_txInProgress) {
                Log.Debug("Transfer progress: {0}%", _txProgressPct);
                _miTxProgressBar.Visible = true;
                _miTxProgressBar.Value = _txProgressPct;
                _miTxProgressBar.ToolTipText = _txProgressPct.ToString();
            } else {
                _miTxProgressBar.Value = 0;
                _miTxProgressBar.Visible = false;
                _miTxProgressBar.ToolTipText = @"Current Transfer Progress";
            }
        }

        /// <summary>
        /// Update the tray icon reflecting the current service and transfer status.
        /// </summary>
        private void UpdateTrayIcon() {
            // if a transfer is running and active show the transfer icon, alternating between its
            // two variants every second ("blinking")
            // NOTE: this is independent of a status change as the blinking should still happen
            // even if the status (file) has not been updated in between
            if (_txInProgress && !_serviceSuspended) {
                if (DateTime.Now.Second % 2 == 0) {
                    _notifyIcon.Icon = _tiTx0;
                } else {
                    _notifyIcon.Icon = _tiTx1;
                }
            }

            // now we can check if a status change occurred and just return otherwise:
            if (!_statusChanged)
                return;

            // show the "stopped" icon if the service process is not running:
            if (!_serviceProcessAlive) {
                _notifyIcon.Icon = _tiStopped;
                return;
            }

            // show the "suspended" icon of the service is in the corresponding state:
            if (_serviceSuspended) {
                _notifyIcon.Icon = _tiSuspended;
                return;
            }

            // if none of the above is true and no transfer is running show the default icon:
            if (!_txInProgress) {
                _notifyIcon.Icon = _tiDefault;
            }
        }
    }
}
