using System;
using System.Diagnostics;
using System.Timers;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ATXCommon.Serializables;
using Microsoft.WindowsAPICodePack.Dialogs;
using Timer = System.Timers.Timer;

namespace ATXTray
{
    public class AutoTxTray : ApplicationContext
    {
        private const string AppTitle = "AutoTx Service Monitor";
        private static readonly Timer AppTimer = new Timer(1000);
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string ConfigFile = Path.Combine(BaseDir, "configuration.xml");
        private static readonly string StatusFile = Path.Combine(BaseDir, "status.xml");
        private static DateTime _statusAge;
        private static ServiceConfig _config;
        private static ServiceStatus _status;

        private static bool _statusChanged = false;
        private static bool _svcRunning = false;
        private static bool _svcSuspended = true;
        private static string _svcSuspendReason;

        private static bool _txInProgress = false;
        private static long _txSize;
        
        private readonly NotifyIcon _notifyIcon = new NotifyIcon();
        private readonly Icon _tiDefault = new Icon("AutoTx.ico");
        private readonly Icon _tiStopped = new Icon("icon-stopped.ico");
        private readonly Icon _tiSuspended = new Icon("icon-suspended.ico");
        private readonly Icon _tiTx0 = new Icon("icon-tx-0.ico");
        private readonly Icon _tiTx1 = new Icon("icon-tx-1.ico");
        private readonly ContextMenuStrip _cmStrip = new ContextMenuStrip();
        private readonly ToolStripMenuItem _miExit = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTitle = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miSvcRunning = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miSvcSuspended = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTxProgress = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTxEnqueue = new ToolStripMenuItem();

        public AutoTxTray() {

            _notifyIcon.Icon = _tiStopped;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += StartNewTransfer;

            // this doesn't work properly, the menu will not close etc. so we disable it for now:
            // _notifyIcon.Click += ShowContextMenu;

            try {
                _config = ServiceConfig.Deserialize(ConfigFile);
                ReadStatus();
            }
            catch (Exception ex) {
                _notifyIcon.ShowBalloonTip(10000, AppTitle,
                    "Unable to read config / status: " + ex.Message, ToolTipIcon.Error);
                System.Threading.Thread.Sleep(10000);
                _notifyIcon.Visible = false;
                Application.Exit();
            }

            _miExit.Text = @"Exit";
            _miExit.Click += MiExitClick;

            _miTitle.Font = new Font(_cmStrip.Font, FontStyle.Bold);
            _miTitle.Text = AppTitle;
            _miTitle.Image = Image.FromFile("AutoTx.ico");
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

            _cmStrip.Items.AddRange(new ToolStripItem[] {
                _miTitle,
                _miSvcRunning,
                _miSvcSuspended,
                _miTxProgress,
                new ToolStripSeparator(),
                _miTxEnqueue,
                new ToolStripSeparator(),
                _miExit
            });

            _notifyIcon.ContextMenuStrip = _cmStrip;

            AppTimer.Elapsed += AppTimerElapsed;
            AppTimer.Enabled = true;
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
            UpdateSvcRunning();

            var heartBeat = "?";
            var serviceRunning = "stopped";
            var txProgress = "No";

            if (_svcRunning) {
                serviceRunning = "OK";
                ReadStatus();
                UpdateSvcSuspended();
                UpdateTxInProgress();
                if ((DateTime.Now - _status.LastStatusUpdate).TotalSeconds < 60)
                    heartBeat = "OK";
                if (_txInProgress)
                    txProgress = _txSize.ToString();
            }

            UpdateTrayIcon();

            if (!_statusChanged)
                return;

            UpdateHoverText(string.Format("AutoTx [svc={0}] [hb={1}] [tx={2}]",
                serviceRunning, heartBeat, txProgress));
        }

        private void MiExitClick(object sender, EventArgs e) {
            _notifyIcon.Visible = false;
            Application.Exit();
        }

        private void ShowContextMenu(object sender, EventArgs e) {
            // just show the menu again, to avoid that clicking the menu item closes the context
            // menu without having to disable the item (which would grey out the text and icon):
            _notifyIcon.ContextMenuStrip.Show();
        }

        private static void StartNewTransfer(object sender, EventArgs e) {
            var dirDialog = new CommonOpenFileDialog {
                Title = @"Select directory to be transferred",
                IsFolderPicker = true,
                EnsurePathExists = true,
                DefaultDirectory = _config.SourceDrive
            };
            if (dirDialog.ShowDialog() == CommonFileDialogResult.Ok) {
                MessageBox.Show("Directory\nselected:\n\n" + dirDialog.FileName,
                    "New transfer confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }
        }

        /// <summary>
        /// Read (or re-read) the service status file if it has changed since last time.
        /// </summary>
        private static void ReadStatus() {
            var age = new FileInfo(StatusFile).LastWriteTime;
            if (age == _statusAge)
                return;

            _statusAge = age;
            _status = ServiceStatus.Deserialize(StatusFile, _config);
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

            _statusChanged = true;
            _svcRunning = curSvcRunState;
            if (_svcRunning) {
                _miSvcRunning.Text = @"Service running.";
                _miSvcRunning.BackColor = Color.LightGreen;
                _miTitle.BackColor = Color.LightGreen;
                _miSvcSuspended.Enabled = true;
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "Service running.", ToolTipIcon.Info);
            } else {
                _miSvcRunning.Text = @"Service NOT RUNNING!";
                _miSvcRunning.BackColor = Color.LightCoral;
                _miTitle.BackColor = Color.LightCoral;
                _miSvcSuspended.Enabled = false;
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "Service stopped.", ToolTipIcon.Error);
            }
        }

        private void UpdateSvcSuspended() {
            // first update the suspend reason as this can possibly change even if the service
            // never leaves the suspended state and we should still display the correct reason:
            if (_svcSuspendReason == _status.LimitReason &&
                _svcSuspended == _status.ServiceSuspended)
                return;

            _statusChanged = true;
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

            _statusChanged = true;
            _txInProgress = _status.TransferInProgress;
            _txSize = _status.CurrentTransferSize;
            if (_txInProgress) {
                _miTxProgress.Text = @"Transfer in progress (size: " + _txSize + ")";
                _miTxProgress.BackColor = Color.LightGreen;
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "New transfer started (size: " + _txSize + ").", ToolTipIcon.Info);
            } else {
                _miTxProgress.Text = @"No transfer running.";
                _miTxProgress.ResetBackColor();
                _notifyIcon.ShowBalloonTip(500, AppTitle,
                    "Transfer completed.", ToolTipIcon.Info);                
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
