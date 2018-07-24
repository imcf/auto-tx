using System;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using NLog;
using Timer = System.Timers.Timer;

namespace ATxCommon.Monitoring
{
    /// <summary>
    /// Abstract load monitoring class, constantly checking the load at the given <see
    /// cref="Interval"/> in a separate (timer-based) thread.
    /// 
    /// The load (depending on the implementation in the derived class) is determined using a <see
    /// cref="PerformanceCounter"/>, and is compared against a configurable <see cref="Limit"/>. If
    /// the load changes from below the limit to above, a <see cref="LoadAboveLimit"/> event will
    /// be raised. If the load has been above the limit and is then dropping below, an <see
    /// cref="OnLoadBelowLimit"/> event will be raised as soon as a given number of consecutive
    /// load measurements (defined via <see cref="Probation"/>) were found to be below the limit.
    /// </summary>
    public abstract class MonitorBase
    {
        protected static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The generic event handler delegate for load monitoring events.
        /// </summary>
        public delegate void EventHandler(object sender, EventArgs e);
        
        /// <summary>
        /// Event raised when the load exceeds the limit for any (i.e. single!) measurement.
        /// </summary>
        public event EventHandler LoadAboveLimit;

        /// <summary>
        /// Event raised when the load is below the configured limit for at least the number of
        /// consecutive measurements configured in <see cref="Probation"/> after having exceeded
        /// this limit before.
        /// </summary>
        public event EventHandler LoadBelowLimit;

        protected readonly Timer MonitoringTimer;
        protected readonly PerformanceCounter PerfCounter;
        protected readonly float[] LoadReadings = {0F, 0F, 0F, 0F};

        private int _interval;
        private int _behaving;
        private int _probation;

        private float _limit;

        /// <summary>
        /// Description string to be used in log messages.
        /// </summary>
        private readonly string _description;


        #region properties

        /// <summary>
        /// Name of the performance counter category, see also <see cref="PerformanceCounter"/>.
        /// </summary>
        protected abstract string Category { get; set; }

        /// <summary>
        /// Current load, averaged of the last four readings.
        /// </summary>
        /// <returns>The average load from the last four readings.</returns>
        public float Load { get; private set; }

        /// <summary>
        /// Flag representing whether the load is considered to be high or low.
        /// </summary>
        public bool HighLoad { get; private set; }

        /// <summary>
        /// Time interval (in ms) after which to update the current load measurement.
        /// </summary>
        public int Interval {
            get => _interval;
            set {
                _interval = value;
                MonitoringTimer.Interval = value;
                Log.Debug("{0} monitoring interval: {1}ms", _description, _interval);
            }
        }

        /// <summary>
        /// Upper limit of the load before it is classified as "high".
        /// </summary>
        public float Limit {
            get => _limit;
            set {
                _limit = value;
                Log.Debug("{0} monitoring limit: {1:0.000}", _description, _limit);
            }
        }

        /// <summary>
        /// Number of cycles where the load value has to be below the limit before it is
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
        /// Indicating whether this load monitor instance is active.
        /// </summary>
        public bool Enabled {
            get => MonitoringTimer.Enabled;
            set {
                Log.Debug("{0} - {1} monitoring.", _description, value ? "enabling" : "disabling");
                MonitoringTimer.Enabled = value;
            }
        }

        /// <summary>
        /// Log level to use for reporting current performance readings, default = Trace.
        /// </summary>
        public LogLevel LogPerformanceReadings { get; set; } = LogLevel.Trace;

        #endregion


        /// <summary>
        /// Create performance counter and initialize it.
        /// </summary>
        /// <param name="counterName">The counter to use for the monitoring, depends on the
        /// category used in the derived class.
        /// </param>
        protected MonitorBase(string counterName) {
            _interval = 250;
            _limit = 0.5F;
            _probation = 40;
            Log.Info("Initializing {0} PerformanceCounter for [{1}].", Category, counterName);
            // assemble the description string to be used in messages:
            _description = $"{Category} {counterName}";
            try {
                PerfCounter = new PerformanceCounter(Category, counterName, "_Total");
                var curLoad = PerfCounter.NextValue();
                Log.Debug("{0} initial value: {1:0.000}", _description, curLoad);
                /* this initialization might be necessary for "Processor" counters, so we just
                 * temporarily disable those calls:
                Thread.Sleep(1000);
                curLoad = _perfCounter.NextValue();
                Log.Debug("{0} current value: {1:0.000}", _description, curLoad);
                 */
                // initialize the load state as high, so we have to pass probation at least once:
                HighLoad = true;
                MonitoringTimer = new Timer(_interval);
                MonitoringTimer.Elapsed += UpdateLoadReadings;
            }
            catch (Exception) {
                Log.Error("{0} monitoring initialization failed!", Category);
                throw;
            }

            Log.Debug("{0} monitoring initialization completed.", _description);
        }

        /// <summary>
        /// Check current load value, update the history of readings and trigger the corresponding
        /// events if the required criteria are met.
        /// </summary>
        private void UpdateLoadReadings(object sender, ElapsedEventArgs e) {
            MonitoringTimer.Enabled = false;
            try {
                // ConstrainedCopy seems to be the most efficient approach to shift the array:
                Array.ConstrainedCopy(LoadReadings, 1, LoadReadings, 0, 3);
                LoadReadings[3] = PerfCounter.NextValue();
                Load = LoadReadings.Average();
                if (LoadReadings[3] > _limit) {
                    if (_behaving > _probation) {
                        // this means the load was considered as "low" before, so raise an event:
                        OnLoadAboveLimit();
                        Log.Trace("{0} ({1:0.00}) violating limit ({2})!",
                            _description, LoadReadings[3], _limit);
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
                MonitoringTimer.Enabled = true;
            }
            Log.Log(LogPerformanceReadings, "{0}: {1:0.000} {2}", _description,
                LoadReadings[3], LoadReadings[3] < Limit ? " [" + _behaving + "]" : "");
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
