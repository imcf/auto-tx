namespace ATxCommon.Monitoring
{
    /// <inheritdoc />
    /// <summary>
    /// Monitoring class for disk I/O, using the disk queue length for measuring the load.
    /// </summary>
    public class PhysicalDisk : MonitorBase
    {
        /// <inheritdoc />
        protected sealed override string Category { get; set; } = "PhysicalDisk";

        /// <inheritdoc />
        /// <summary>
        /// Create performance counter and initialize it.
        /// </summary>
        /// <param name="counterName">The counter to use for the monitoring, default is the
        /// overall "Avg. Disk Queue Length", other reasonable options are the corresponding read
        /// or write queues ("Avg. Disk Read Queue Length" and "Avg. Disk Write Queue Length").
        /// </param>
        public PhysicalDisk(string counterName = "Avg. Disk Queue Length")
            : base(counterName) {
        }
    }
}