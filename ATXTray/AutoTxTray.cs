using System;
using System.Diagnostics;
using System.Timers;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ATXSerializables;
using Timer = System.Timers.Timer;

namespace ATXTray
{
    public class AutoTxTray : ApplicationContext
    {
        private static readonly Timer AppTimer = new Timer(1000);
        private static readonly string _serviceDir = @"C:\Tools\AutoTx";
        private static readonly string ConfigFile = Path.Combine(_serviceDir, "configuration.xml");
        private static readonly string StatusFile = Path.Combine(_serviceDir, "status.xml");
        private static DateTime _statusAge;
        private static ServiceConfig _config;
        private static ServiceStatus _status;

        private static bool _statusChanged = false;
        private static bool _svcRunning = false;
        private static bool _svcSuspended = true;
        
        private readonly NotifyIcon _notifyIcon = new NotifyIcon();
        private readonly ContextMenuStrip _cmStrip = new ContextMenuStrip();
        private readonly ToolStripMenuItem _miExit = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miTitle = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miSvcRunning = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _miSvcSuspended = new ToolStripMenuItem();

        public AutoTxTray() {
            _notifyIcon.Icon = new Icon("AutoTx.ico");

            _config = ServiceConfig.Deserialize(ConfigFile);

            _miExit.Text = @"Exit";
            _miExit.Click += MiExitClick;

            _miTitle.Font = new Font(_cmStrip.Font, FontStyle.Bold);
            _miTitle.Text = @"AutoTx Service Monitor";
            _miTitle.Image = Image.FromFile("AutoTx.ico");
            _miTitle.BackColor = Color.LightCoral;
            _miTitle.Click += ShowContextMenu;

            _miSvcRunning.Text = @"Service NOT RUNNING!";
            _miSvcRunning.BackColor = Color.LightCoral;
            _miSvcRunning.Click += ShowContextMenu;

            _miSvcSuspended.Text = @"No limits apply, service active.";
            _miSvcSuspended.Click += ShowContextMenu;

            _cmStrip.Items.AddRange(new ToolStripItem[] {
                _miTitle,
                _miSvcRunning,
                _miSvcSuspended,
                _miExit
            });

            _notifyIcon.ContextMenuStrip = _cmStrip;
            _notifyIcon.Visible = true;

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
            ReadStatus();
            UpdateSvcRunning();
            string heartBeatText = "OK";
            var heartBeat = (int) (DateTime.Now - _status.LastStatusUpdate).TotalSeconds;
            if (heartBeat > 60)
                heartBeatText = "--";

            if (!_statusChanged)
                return;

            string serviceRunning = @"stopped";
            if (_svcRunning) serviceRunning = @"OK";

            UpdateHoverText(string.Format("AutoTx [svc={0}] [hb={1}] [tx={2}]",
                serviceRunning, heartBeatText, _status.TransferInProgress));
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

        /// <summary>
        /// Read (or re-read) the service status file if it has changed since last time.
        /// </summary>
        private void ReadStatus() {
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
                _notifyIcon.ShowBalloonTip(500, "AutoTx Monitor",
                    "Service started.", ToolTipIcon.Info);
            } else {
                _miSvcRunning.Text = @"Service NOT RUNNING!";
                _miSvcRunning.BackColor = Color.LightCoral;
                _miTitle.BackColor = Color.LightCoral;
                _miSvcSuspended.Enabled = false;
                _notifyIcon.ShowBalloonTip(500, "AutoTx Monitor",
                    "Service stopped.", ToolTipIcon.Error);
            }
        }
    }
}
