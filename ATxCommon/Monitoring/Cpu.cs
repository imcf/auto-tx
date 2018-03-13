using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using NLog;
using Timer = System.Timers.Timer;

namespace ATxCommon.Monitoring
{
    /// <summary>
    /// CPU load monitoring class, constantly checking the current load at the given <see
    /// cref="Interval"/> using a separate timer (thus running in its own thread).
    /// 
    /// The load is determined using a <see cref="PerformanceCounter"/>, and is compared against
    /// a configurable <see cref="Limit"/>. If the load changes from below the limit to above, a
    /// <see cref="LoadAboveLimit"/> event will be raised. If the load has been above the limit
    /// and is then dropping below, an <see cref="OnLoadBelowLimit"/> event will be raised as soon
    /// as a given number of consecutive measurements (defined via <see cref="Probation"/>) were
    /// found to be below the limit.
    /// </summary>
    public class Cpu
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The generic event handler delegate for CPU events.
        /// </summary>
        public delegate void EventHandler(object sender, EventArgs e);
        
        /// <summary>
        /// Event raised when the CPU load exceeds the configured limit for any measurement.
        /// </summary>
        public event EventHandler LoadAboveLimit;

        /// <summary>
        /// Event raised when the CPU load is below the configured limit for at least four
        /// consecutive measurements after having exceeded this limit before.
        /// </summary>
        public event EventHandler LoadBelowLimit;

        private readonly Timer _monitoringTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly float[] _loadReadings = {0F, 0F, 0F, 0F};

        private int _interval;
        private int _limit;
        private int _behaving;
        private int _probation;


        /// <summary>
        /// Current CPU load (usage percentage over all cores), averaged of the last four readings.
        /// </summary>
        /// <returns>The average CPU load from the last four readings.</returns>
        public float Load { get; private set; }

        /// <summary>
        /// How often (in ms) to check the CPU load.
        /// </summary>
        public int Interval {
            get => _interval;
            set {
                _interval = value;
                _monitoringTimer.Interval = value;
                Log.Debug("CPU monitoring interval: {0}", _interval);
            }
        }

        /// <summary>
        /// Upper limit of CPU load (usage in % over all cores) before it is classified as "high".
        /// </summary>
        public int Limit {
            get => _limit;
            set {
                _limit = value;
                Log.Debug("CPU monitoring limit: {0}", _limit);
            }
        }

        /// <summary>
        /// Number of cycles where the CPU load value has to be below the limit before it is
        /// classified as "low" again.
        /// </summary>
        public int Probation {
            get => _probation;
            set {
                _probation = value;
                Log.Debug("CPU monitoring probation cycles when violating limit: {0}", _probation);
            }
        }
        
        /// <summary>
        /// Indicating whether the CPU load monitoring is active.
        /// </summary>
        public bool Enabled {
            get => _monitoringTimer.Enabled;
            set {
                Log.Debug("{0} CPU monitoring.", value ? "Enabling" : "Disabling");
                _monitoringTimer.Enabled = value;
            }
        }



        /// <summary>
        /// Create performance counter and initialize it.
        /// </summary>
        public Cpu() {
            _interval = 250;
            _limit = 25;
            _probation = 40;
            Log.Debug("Initializing CPU monitoring...");
            try {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                Log.Debug("CPU monitoring initializing PerformanceCounter (takes one second)...");
                _cpuCounter.NextValue();
                Thread.Sleep(1000);
                Log.Debug("CPU monitoring current load: {0:0.0}", _cpuCounter.NextValue());
                // _monitoringTimer = new Timer(_interval);
                _monitoringTimer = new Timer(_interval);
                _monitoringTimer.Elapsed += UpdateCpuLoad;
            }
            catch (Exception) {
                Log.Error("Initializing CPU monitoring failed!");
                throw;
            }

            Log.Debug("Initializing CPU monitoring completed.");
        }

        /// <summary>
        /// Check current CPU load, update the history of readings and trigger the corresponding
        /// events if the required criteria are met.
        /// </summary>
        private void UpdateCpuLoad(object sender, ElapsedEventArgs e) {
            _monitoringTimer.Enabled = false;
            try {
                // ConstrainedCopy seems to be the most efficient approach to shift the array:
                Array.ConstrainedCopy(_loadReadings, 1, _loadReadings, 0, 3);
                _loadReadings[3] = _cpuCounter.NextValue();
                Load = _loadReadings.Average();
                if (_loadReadings[3] > _limit) {
                    if (_behaving > _probation) {
                        // this means the load was considered as "low" before, so raise an event:
                        OnLoadAboveLimit();
                        Log.Trace("CPU load ({0:0.0}) violating limit ({1})!", _loadReadings[3], _limit);
                    } else if (_behaving > 0) {
                        // this means we were still in probation, so no need to trigger again...
                        Log.Trace("Resetting behaving counter to 0 (was {0}).", _behaving);
                    }
                  _behaving = 0;
                } else {
                    _behaving++;
                    if (_behaving == _probation) {
                        Log.Trace("CPU load below limit for {0} cycles, passing probation!", _probation);
                        OnLoadBelowLimit();
                    } else if (_behaving > _probation) {
                        Log.Trace("CPU load behaving well since {0} cycles.", _behaving);
                    } else if (_behaving < 0) {
                        Log.Warn("Integer wrap around happened, resetting probation counter!");
                        _behaving = _probation + 1;
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("UpdateCpuLoad failed: {0}", ex.Message);
            }
            finally {
                _monitoringTimer.Enabled = true;
            }
            Log.Trace("CPU load: {0:0.0} {1}", _loadReadings[3],
                _loadReadings[3] < Limit ? " [" + _behaving + "]" : "");
        }

        /// <summary>
        /// Raise the "LoadAboveLimit" event.
        /// </summary>
        protected virtual void OnLoadAboveLimit() {
            LoadAboveLimit?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raise the "LoadBelowLimit" event.
        /// </summary>
        protected virtual void OnLoadBelowLimit() {
            LoadBelowLimit?.Invoke(this, EventArgs.Empty);
        }
    }
}
