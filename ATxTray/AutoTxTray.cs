﻿using System;
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
        private static bool _svcRunning = false;
        private static bool _svcSuspended = true;
        private static string _svcSuspendReason;

        private static bool _txInProgress = false;
        private static long _txSize;
        private static int _txProgressPct;
        
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

        private static TaskDialog _confirmDialog;
        private static DirectoryInfo _selectedDir;

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
            _notifyIcon.DoubleClick += StartNewTransfer;

            // this doesn't work properly, the menu will not close etc. so we disable it for now:
            // _notifyIcon.Click += ShowContextMenu;

            Log.Trace("Trying to read service config and status files...");
            try {
                _config = ServiceConfig.Deserialize(Path.Combine(baseDir, "configuration.xml"));
                ReadStatus();
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
            fsw.Changed += StatusUpdated;
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
            _miTxEnqueue.Click += StartNewTransfer;

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

        private void AppTimerElapsed(object sender, ElapsedEventArgs e) {
            if (_status == null) {
                AutoTxTrayExit();
                return;
            }

            UpdateSvcRunning();
            ReadStatus();  // update the status no matter if the service process is running

            var serviceRunning = "stopped";
            var heartBeat = "?";
            var txProgress = "No";

            if (_svcRunning) {
                serviceRunning = "OK";
                if ((DateTime.Now - _status.LastStatusUpdate).TotalSeconds <= 60)
                    heartBeat = "OK";
                if (_txInProgress)
                    txProgress = $"{_txProgressPct}%";
            }

            UpdateHoverText($"AutoTx [svc={serviceRunning}] [hb={heartBeat}] [tx={txProgress}]");

            if (!_statusChanged)
                return;

            UpdateSvcSuspended();
            UpdateTxProgressBar();
            UpdateTxInProgress();

            UpdateTrayIcon();
            _statusChanged = false;
        }

        private static void StatusUpdated(object sender, FileSystemEventArgs e) {
            _statusFileChanged = true;
        }

        private void ShowContextMenu(object sender, EventArgs e) {
            // just show the menu again, to avoid that clicking the menu item closes the context
            // menu without having to disable the item (which would grey out the text and icon):
            _notifyIcon.ContextMenuStrip.Show();
        }

        private static void StartNewTransfer(object sender, EventArgs e) {
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

            ReviewNewTxDialog();
        }

        private static void ReviewNewTxDialog() {
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
                    SubmitDirForTransfer();
            }
        }

        private static void TaskDialogOpened(object sender, EventArgs e) {
            var td = sender as TaskDialog;
            td.Icon = TaskDialogStandardIcon.Shield;
        }

        private static void ConfirmAcceptClick(object sender, EventArgs e) {
            _confirmDialog.Close();
            SubmitDirForTransfer();
        }

        private static void ConfirmChangeClick(object sender, EventArgs e) {
            _confirmDialog.Close();
            Log.Debug("User wants to change directory choice.");
            StartNewTransfer(sender, e);
        }

        private static void SubmitDirForTransfer() {
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
        private static void ReadStatus() {
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
        private static bool ServiceProcessRunning() {
            var plist = Process.GetProcessesByName("AutoTx");
            return plist.Length > 0;
        }

        private void UpdateSvcRunning() {
            var curSvcRunState = ServiceProcessRunning();
            if (_svcRunning == curSvcRunState)
                return;

            _svcRunning = curSvcRunState;
            if (_svcRunning) {
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

        private void UpdateSvcSuspended() {
            // first update the suspend reason as this can possibly change even if the service
            // never leaves the suspended state and we should still display the correct reason:
            if (_svcSuspendReason == _status.LimitReason &&
                _svcSuspended == _status.ServiceSuspended)
                return;

            _svcSuspended = _status.ServiceSuspended;
            _svcSuspendReason = _status.LimitReason;
            if (_svcSuspended) {
                _miSvcSuspended.Text = @"Service suspended, reason: " + _svcSuspendReason;
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

        private void UpdateTxInProgress() {
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

        private void UpdateTrayIcon() {
            if (_txInProgress &&
                !_svcSuspended) {
                if (DateTime.Now.Second % 2 == 0) {
                    _notifyIcon.Icon = _tiTx0;
                } else {
                    _notifyIcon.Icon = _tiTx1;
                }
            }

            if (!_statusChanged)
                return;

            if (!_svcRunning) {
                _notifyIcon.Icon = _tiStopped;
                return;
            }

            if (_svcSuspended) {
                _notifyIcon.Icon = _tiSuspended;
                return;
            }

            if (!_txInProgress) {
                _notifyIcon.Icon = _tiDefault;
            }
        }
    }
}
