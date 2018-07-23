﻿using System;
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
    /// cref="Interval"/> in a separate (timer-based) thread.
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
        /// Performance counter category name, <see cref="PerformanceCounter"/> for details.
        /// </summary>
        private const string Category = "Processor";
        
        /// <summary>
        /// Description string to be used in log messages.
        /// </summary>
        private readonly string _description;

        #region properties

        /// <summary>
        /// Current CPU load (usage percentage over all cores), averaged of the last four readings.
        /// </summary>
        /// <returns>The average CPU load from the last four readings.</returns>
        public float Load { get; private set; }

        /// <summary>
        /// Flag representing whether the load is considered to be high or low.
        /// </summary>
        public bool HighLoad { get; private set; }

        /// <summary>
        /// How often (in ms) to check the CPU load.
        /// </summary>
        public int Interval {
            get => _interval;
            set {
                _interval = value;
                _monitoringTimer.Interval = value;
                Log.Debug("{0} monitoring interval: {1}ms", _description, _interval);
            }
        }

        /// <summary>
        /// Upper limit of CPU load (usage in % over all cores) before it is classified as "high".
        /// </summary>
        public int Limit {
            get => _limit;
            set {
                _limit = value;
                Log.Debug("{0} monitoring limit: {1}", _description, _limit);
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
                Log.Debug("{0} monitoring probation cycles when violating limit: {1}",
                    _description, _probation);
            }
        }
        
        /// <summary>
        /// Indicating whether the CPU load monitoring is active.
        /// </summary>
        public bool Enabled {
            get => _monitoringTimer.Enabled;
            set {
                Log.Debug("{0} - {1} monitoring.", _description, value ? "enabling" : "disabling");
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
        public Cpu(string counterName = "% Processor Time") {
            _interval = 250;
            _limit = 25;
            _probation = 40;
            Log.Info("Initializing {0} performance monitoring for [{1}].", Category, counterName);
            // assemble the description string to be used in messages:
            _description = $"{Category} {counterName}";
            try {
                Log.Debug("{0} monitoring initializing PerformanceCounter (takes one second)...", Category);
                _cpuCounter = new PerformanceCounter(Category, counterName, "_Total");
                var curLoad = _cpuCounter.NextValue();
                Log.Debug("{0} initial value: {1:0.0}", _description, curLoad);
                Thread.Sleep(1000);
                curLoad = _cpuCounter.NextValue();
                Log.Debug("{0} current value: {1:0.0}", _description, curLoad);
                // now initialize the load state:
                HighLoad = curLoad > _limit;
                _monitoringTimer = new Timer(_interval);
                _monitoringTimer.Elapsed += UpdateCpuLoad;
            }
            catch (Exception) {
                Log.Error("{0} monitoring initialization failed!", Category);
                throw;
            }

            Log.Debug("{0} monitoring initialization completed.", _description);
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
                        Log.Trace("{0} ({1:0.0}) violating limit ({2})!",
                            _description, _loadReadings[3], _limit);
                    } else if (_behaving > 0) {
                        // this means we were still in probation, so no need to trigger again...
                        Log.Trace("{0}: resetting behaving counter (was {1}).",
                            _description, _behaving);
                    }
                  _behaving = 0;
                } else {
                    _behaving++;
                    if (_behaving == _probation) {
                        Log.Trace("{0} below limit for {1} cycles, passing probation!",
                            _description, _probation);
                        OnLoadBelowLimit();
                    } else if (_behaving > _probation) {
                        Log.Trace("{0} behaving well since {1} cycles.",
                            _description, _behaving);
                    } else if (_behaving < 0) {
                        Log.Info("{0}: integer wrap around happened, resetting probation " +
                                 "counter (no reason to worry).", _description);
                        _behaving = _probation + 1;
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("Updating {0} counters failed: {1}", _description, ex.Message);
            }
            finally {
                _monitoringTimer.Enabled = true;
            }
            Log.Log(LogPerformanceReadings, "{0}: {1:0.0} {2}", _description,
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
