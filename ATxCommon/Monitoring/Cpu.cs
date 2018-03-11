using System;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using NLog;

namespace ATxCommon.Monitoring
{
    public class Cpu
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly Timer _monitoringTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly float[] _loadReadings = {0F, 0F, 0F, 0F};
        private float _load;

        public float Load() => _load;

        public Cpu() {
            Log.Debug("Initializing CPU monitoring...");
            try {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _monitoringTimer = new Timer(250);
                _monitoringTimer.Elapsed += UpdateCpuLoad;
                _monitoringTimer.Enabled = true;
            }
            catch (Exception ex) {
                Log.Error("Initializing CPU monitoring completed ({1}): {0}", ex.Message, ex.GetType());
                throw;
            }

            Log.Debug("Initializing CPU monitoring completed.");
        }

        private void UpdateCpuLoad(object sender, ElapsedEventArgs e) {
            _monitoringTimer.Enabled = false;
            try {
                Log.Trace("load values: {0}, average: {1}", string.Join(", ", _loadReadings), _load);
                // ConstrainedCopy seems to be the most efficient approach to shift the array:
                Array.ConstrainedCopy(_loadReadings, 1, _loadReadings, 0, 3);
                _loadReadings[3] = _cpuCounter.NextValue();
                _load = _loadReadings.Average();
                Log.Trace("load values: {0}, average: {1}", string.Join(", ", _loadReadings), _load);
            }
            catch (Exception ex) {
                Log.Error("UpdateCpuLoad failed: {0}", ex.Message);
            }
            finally {
                _monitoringTimer.Enabled = true;
            }
        }

    }
}
