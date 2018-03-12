using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using NLog;
using Timer = System.Timers.Timer;

namespace ATxCommon.Monitoring
{
    public class Cpu
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly Timer _monitoringTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly float[] _loadReadings = {0F, 0F, 0F, 0F};

        private float _load;
        private int _interval;
        private int _limit;
        private int _behaving;
        private int _probation;

        /// <summary>
        /// Current CPU load (usage percentage over all cores).
        /// </summary>
        /// <returns>The average CPU load from the last four readings.</returns>
        public float Load() => _load;

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
                Log.Debug("CPU monitoring initializing PerformanceCounter (takes 1s)...");
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


        private void UpdateCpuLoad(object sender, ElapsedEventArgs e) {
            _monitoringTimer.Enabled = false;
            try {
                // ConstrainedCopy seems to be the most efficient approach to shift the array:
                Array.ConstrainedCopy(_loadReadings, 1, _loadReadings, 0, 3);
                _loadReadings[3] = _cpuCounter.NextValue();
                _load = _loadReadings.Average();
                if (_loadReadings[3] > _limit) {
                    Log.Debug("CPU load ({0}) violating limit ({1})!", _loadReadings[3], _limit);
                    _behaving = 0;
                    // TODO: fire callback for violating load limit
                } else {
                    _behaving++;
                    if (_behaving == _probation) {
                        Log.Debug("CPU load below limit for {0} cycles, passing probation!", _probation);
                        // TODO: fire callback for load behaving well
                    } else if (_behaving > _probation) {
                        Log.Trace("CPU load behaving well since {0} cycles.", _behaving);
                    } else if (_behaving < 0) {
                        Log.Warn("Integer wrap around happened, resetting probation counter!");
                        _behaving = 0;
                    }
                }
            }
            catch (Exception ex) {
                Log.Error("UpdateCpuLoad failed: {0}", ex.Message);
            }
            finally {
                _monitoringTimer.Enabled = true;
            }
            Log.Info("CPU load: {0:0.0} {1}", _loadReadings[3], _loadReadings[3] < Limit ? " [" + _behaving + "]" : "");
            Log.Trace("load values: {0}, average: {1}", string.Join(", ", _loadReadings), _load);
        }

    }
}
