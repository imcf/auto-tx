namespace ATxCommon.Monitoring
{
    /// <inheritdoc />
    /// <summary>
    /// Monitoring class for CPU, using the idle/non-idle time fraction for measuring the load.
    /// </summary>
    public class Cpu : MonitorBase
    {
        /// <inheritdoc />
        protected sealed override string Category { get; set; } = "Processor";

        /// <inheritdoc />
        /// <summary>
        /// Create performance counter and initialize it.
        /// </summary>
        /// <param name="counterName">The counter to use for the monitoring, default is the
        /// overall "% Processor Time".
        /// </param>
        public Cpu(string counterName = "% Processor Time")
            : base(counterName) {
        }
    }
}