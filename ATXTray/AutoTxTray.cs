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
        private readonly ToolStripMenuItem _mi1 = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _mi2 = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _mi3 = new ToolStripMenuItem();

        public AutoTxTray() {
            _notifyIcon.Icon = new Icon("AutoTx.ico");
            var bold = new Font(_cmStrip.Font, FontStyle.Bold);

            _config = ServiceConfig.Deserialize(ConfigFile);

            _mi1.Text = @"Exit";
            _mi1.Click += _mi1_Click;

            _mi2.Font = bold;
            _mi2.Click += ShowContextMenu;

            _mi3.Font = bold;
            _mi3.Text = @"service active (no limitations apply)";
            _mi3.Click += ShowContextMenu;

            _cmStrip.Items.AddRange(new ToolStripItem[] {
                _mi3,
                _mi2,
                _mi1
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

        private void _mi1_Click(object sender, EventArgs e) {
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
                _mi2.Text = @"AutoTx service running";
                _mi2.Image = Image.FromFile("AutoTx.ico");
                _mi2.BackColor = Color.LightGreen;
                _mi3.Enabled = true;
            } else {
                _mi2.Image = null;
                _mi2.BackColor = Color.LightCoral;
                _mi2.Text = @"AutoTx service NOT RUNNING";
                _mi3.Enabled = false;
            }
        }
    }
}
