using System;
using System.Diagnostics;
using System.Linq;
// using System.Threading;
using System.Timers;
using NLog;
using Timer = System.Timers.Timer;

namespace ATxCommon.Monitoring
{
    /// <summary>
    /// Load monitoring class for physical disks, constantly checking the queue length at the
    /// given <see cref="Interval"/> in a separate (timer-based) thread.
    /// 
    /// The load (=queue length) is determined using a <see cref="PerformanceCounter"/>, and is
    /// compared against a configurable <see cref="Limit"/>. If the load changes from below the
    /// limit to above, a <see cref="LoadAboveLimit"/> event will be raised. If the load has been
    /// above the limit and is then dropping below, an <see cref="OnLoadBelowLimit"/> event will
    /// be raised as soon as a given number of consecutive load measurements (defined via <see
    /// cref="Probation"/>) were found to be below the limit.
    /// </summary>
    public class PhysicalDisk
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The generic event handler delegate for PhysicalDisk events.
        /// </summary>
        public delegate void EventHandler(object sender, EventArgs e);
        
        /// <summary>
        /// Event raised when the PhysicalDisk load exceeds the limit for any measurement.
        /// </summary>
        public event EventHandler LoadAboveLimit;

        /// <summary>
        /// Event raised when the PhysicalDisk load is below the configured limit for at least
        /// the number of consecutive measurements configured in <see cref="Probation"/> after
        /// having exceeded this limit before.
        /// </summary>
        public event EventHandler LoadBelowLimit;

        private readonly Timer _monitoringTimer;
        private readonly PerformanceCounter _diskQueueLength;
        private readonly float[] _loadReadings = {0F, 0F, 0F, 0F};

        private int _interval;
        private int _behaving;
        private int _probation;

        private float _limit;



        #region properties

        /// <summary>
        /// Current PhysicalDisk Queue Length, averaged of the last four readings.
        /// </summary>
        /// <returns>The average PhysicalDisk Queue Length from the last four readings.</returns>
        public float Load { get; private set; }

        /// <summary>
        /// Flag representing whether the load is considered to be high or low.
        /// </summary>
        public bool HighLoad { get; private set; }

        /// <summary>
        /// How often (in ms) to check the PhysicalDisk Queue Length.
        /// </summary>
        public int Interval {
            get => _interval;
            set {
                _interval = value;
                _monitoringTimer.Interval = value;
                Log.Debug("PhysicalDisk Queue Length monitoring interval: {0}", _interval);
            }
        }

        /// <summary>
        /// Upper limit of PhysicalDisk load before it is classified as "high".
        /// </summary>
        public float Limit {
            get => _limit;
            set {
                _limit = value;
                Log.Debug("PhysicalDisk monitoring limit: {0:0.000}", _limit);
            }
        }

        /// <summary>
        /// Number of cycles where the PhysicalDisk load value has to be below the limit before it is
        /// classified as "low" again.
        /// </summary>
        public int Probation {
            get => _probation;
            set {
                _probation = value;
                Log.Debug("PhysicalDisk monitoring probation cycles when violating limit: {0}", _probation);
            }
        }
        
        /// <summary>
        /// Indicating whether the PhysicalDisk load monitoring is active.
        /// </summary>
        public bool Enabled {
            get => _monitoringTimer.Enabled;
            set {
                Log.Debug("{0} PhysicalDisk monitoring.", value ? "Enabling" : "Disabling");
                _monitoringTimer.Enabled = value;
            }
        }

        /// <summary>
        /// Log level to use for reporting current performance readings.
        /// </summary>
        public LogLevel LogPerformanceReadings { get; set; } = LogLevel.Trace;

        #endregion



        /// <summary>
        /// Create performance counter and initialize it.
        /// </summary>
        /// <param name="counterName">The counter to use for the monitoring, default is the
        /// overall "Avg. Disk Queue Length", other reasonable options are the corresponding read
        /// or write queues ("Avg. Disk Read Queue Length" and "Avg. Disk Write Queue Length").
        /// </param>
        public PhysicalDisk(string counterName = "Avg. Disk Queue Length") {
            _interval = 250;
            _limit = 0.5F;
            _probation = 40;
            Log.Info($"Initializing PhysicalDisk performance monitoring for [{counterName}]...");
            try {
                Log.Debug("PhysicalDisk monitoring initializing PerformanceCounter...");
                _diskQueueLength = new PerformanceCounter("PhysicalDisk", counterName, "_Total");
                var curLoad = _diskQueueLength.NextValue();
                Log.Debug("PhysicalDisk Queue Length initial value: {0:0.000}", curLoad);
                /* this initialization doesn't seem to be necessary for PhysicalDisk, so we just
                 * disable those calls for now:
                Thread.Sleep(1000);
                curLoad = _diskQueueLength.NextValue();
                Log.Debug("PhysicalDisk monitoring current queue length: {0:0.000}", curLoad);
                 */
                // now initialize the load state:
                HighLoad = curLoad > _limit;
                _monitoringTimer = new Timer(_interval);
                _monitoringTimer.Elapsed += UpdatePhysicalDiskLoad;
            }
            catch (Exception) {
                Log.Error("Initializing PhysicalDisk monitoring failed!");
                throw;
            }

            Log.Debug("Initializing PhysicalDisk monitoring completed.");
        }

        /// <summary>
        /// Check current PhysicalDisk queue length, update the history of readings and trigger
        /// the corresponding events if the required criteria are met.
        /// </summary>
        private void UpdatePhysicalDiskLoad(object sender, ElapsedEventArgs e) {
            _monitoringTimer.Enabled = false;
            try {
                // ConstrainedCopy seems to be the most efficient approach to shift the array:
                Array.ConstrainedCopy(_loadReadings, 1, _loadReadings, 0, 3);
                _loadReadings[3] = _diskQueueLength.NextValue();
                Load = _loadReadings.Average();
                if (_loadReadings[3] > _limit) {
                    if (_behaving > _probation) {
                        // this means the load was considered as "low" before, so raise an event:
                        OnLoadAboveLimit();
                        Log.Trace("PhysicalDisk Queue Length ({0:0.00}) violating limit ({1})!",
                            _loadReadings[3], _limit);
                    } else if (_behaving > 0) {
                        // this means we were still in probation, so no need to trigger again...
                        Log.Trace("PhysicalDisk: resetting behaving counter (was {0}).", _behaving);
                    }
                    _behaving = 0;
                } else {
                    _behaving++;
                    if (_behaving == _probation) {
                        Log.Trace("PhysicalDisk Queue Length below limit for {0} cycles, " +
                                  "passing probation!", _probation);
                        OnLoadBelowLimit();
                    } else if (_behaving > _probation) {
                        Log.Trace("PhysicalDisk Queue Length behaving well since {0} cycles.",
                            _behaving);
                    } else if (_behaving < 0) {
                        Log.Info("PhysicalDisk Queue Length: integer wrap around happened, " +
                                 "resetting probation counter (no reason to worry).");
                        _behaving = _probation + 1;
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("UpdatePhysicalDiskLoad failed: {0}", ex.Message);
            }
            finally {
                _monitoringTimer.Enabled = true;
            }
            Log.Log(LogPerformanceReadings, "PhysicalDisk Queue Length: {0:0.000} {1}",
                _loadReadings[3], _loadReadings[3] < Limit ? " [" + _behaving + "]" : "");
        }

        /// <summary>
        /// Raise the "LoadAboveLimit" event.
        /// </summary>
        protected virtual void OnLoadAboveLimit() {
            HighLoad = true;
            LoadAboveLimit?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raise the "LoadBelowLimit" event.
        /// </summary>
        protected virtual void OnLoadBelowLimit() {
            HighLoad = false;
            LoadBelowLimit?.Invoke(this, EventArgs.Empty);
        }
    }
}