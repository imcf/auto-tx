using System;
using System.IO;
using System.Xml.Serialization;
using NLog;

namespace ATxCommon.Serializables
{
    /// <summary>
    /// AutoTx service status class.
    /// </summary>
    [Serializable]
    public class ServiceStatus
    {
        private string _storageFile; // remember where we came from
        private ServiceConfig _config;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private DateTime _lastStatusUpdate;
        private DateTime _lastStorageNotification;
        private DateTime _lastAdminNotification;
        private DateTime _lastGraceNotification;

        private string _statusDescription;
        private string _currentTransferSrc;
        private string _txTargetUser;

        private bool _transferInProgress;
        private bool _serviceSuspended;
        private bool _cleanShutdown;

        private long _currentTransferSize;
        private long _transferredBytesCompleted;
        private long _transferredBytesCurrentFile;

        private int _currentTransferPercent;


        #region constructor, serializer and deserializer

        /// <summary>
        /// The constructor, setting default values.
        /// </summary>
        private ServiceStatus() {
            _currentTransferSrc = "";
            _txTargetUser = "";
            _transferInProgress = false;
            _transferredBytesCompleted = 0;
            _transferredBytesCurrentFile = 0;
            _currentTransferPercent = 0;
        }

        public void Serialize() {
            /* During de-serialization, the setter methods get called as well but
             * we should not serialize until the deserialization has completed.
             * As the storage file name will only be set after this, it is sufficient
             * to test for this (plus, we can't serialize anyway without it).
             */
            if (_storageFile == null) {
                Log.Trace("File name for XML serialization is not set, doing nothing!");
                return;
            }
            Log.Trace("Serializing status...");
            // update the timestamp:
            LastStatusUpdate = DateTime.Now;
            try {
                var xs = new XmlSerializer(GetType());
                var writer = File.CreateText(_storageFile);
                xs.Serialize(writer, this);
                writer.Flush();
                writer.Close();
            }
            catch (Exception ex) {
                Log.Error("Error in Serialize(): {0}", ex.Message);
            }
            Log.Trace("Finished serializing [{0}].", _storageFile);
        }

        /// <summary>
        /// Wrapper to serialize XML if time since last is above threshold (default = 1 min).
        /// </summary>
        public void SerializeHeartbeat(int timeout=60) {
            if (TimeUtils.SecondsSince(_lastStatusUpdate) >= timeout)
                Serialize();
        }

        public static ServiceStatus Deserialize(string file, ServiceConfig config) {
            Log.Trace("Trying to deserialize status XML file [{0}].", file);
            ServiceStatus status;

            var xs = new XmlSerializer(typeof(ServiceStatus));
            try {
                var reader = File.OpenText(file);
                status = (ServiceStatus) xs.Deserialize(reader);
                reader.Close();
                Log.Trace("Finished deserializing service status XML file.");
            }
            catch (Exception) {
                // if reading the status XML fails, we return an empty (new) one
                status = new ServiceStatus();
                Log.Warn("Deserializing [{0}] failed, creating new status using defauls.", file);
            }
            status._config = config;
            ValidateStatus(status);
            // now set the storage filename:
            status._storageFile = file;
            return status;
        }

        #endregion constructor, serializer and deserializer

        
        #region getter / setter methods

        /// <summary>
        /// Timestamp indicating when the status has been updated last ("heartbeat").
        /// </summary>
        [XmlElement("LastStatusUpdate", DataType = "dateTime")]
        public DateTime LastStatusUpdate {
            get => _lastStatusUpdate;
            set => _lastStatusUpdate = value;
        }

        /// <summary>
        /// Timestamp indicating when the last storage notification has been sent.
        /// </summary>
        [XmlElement("LastStorageNotification", DataType = "dateTime")]
        public DateTime LastStorageNotification {
            get => _lastStorageNotification;
            set {
                _lastStorageNotification = value;
                Serialize();
            }
        }

        /// <summary>
        /// Timestamp indicating when the last admin notification has been sent.
        /// </summary>
        [XmlElement("LastAdminNotification", DataType = "dateTime")]
        public DateTime LastAdminNotification {
            get => _lastAdminNotification;
            set {
                _lastAdminNotification = value;
                Serialize();
            }
        }

        /// <summary>
        /// Timestamp indicating when the last notification on expired folders has been sent.
        /// </summary>
        [XmlElement("LastGraceNotification", DataType = "dateTime")]
        public DateTime LastGraceNotification {
            get => _lastGraceNotification;
            set {
                _lastGraceNotification = value;
                Serialize();
            }
        }

        /// <summary>
        /// String indicating why the service is currently suspended (empty if not suspended).
        /// </summary>
        public string StatusDescription {
            get => _statusDescription;
            set {
                if (_statusDescription == value) return;

                _statusDescription = value;
                Log.Trace("StatusDescription was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// The full path to the folder currently being transferred.
        /// </summary>
        public string CurrentTransferSrc {
            get => _currentTransferSrc;
            set {
                _currentTransferSrc = value;
                Log.Trace("CurrentTransferSrc was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// The user account name that should receive the data from the currently running transfer.
        /// See also <seealso cref="TxTargetTmp"/> on details for assembling the path that
        /// is being used as a temporary location while a transfer is in progress.
        /// </summary>
        public string TxTargetUser {
            get => _txTargetUser;
            set {
                _txTargetUser = value;
                Log.Trace("TxTargetUser was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Flag indicating whether the service is currently suspended.
        /// </summary>
        public bool ServiceSuspended {
            get => _serviceSuspended;
            set {
                if (_serviceSuspended == value) return;

                _serviceSuspended = value;
                Log.Trace("ServiceSuspended was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Flag indicating whether a transfer is currently running.
        /// </summary>
        public bool TransferInProgress {
            get => _transferInProgress;
            set {
                _transferInProgress = value;
                Log.Trace("FilecopyFinished was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Indicates whether the service was cleanly shut down (false while the service is running).
        /// </summary>
        public bool CleanShutdown {
            get => _cleanShutdown;
            set {
                _cleanShutdown = value;
                Serialize();
            }
        }

        /// <summary>
        /// The full size of the current transfer in bytes.
        /// </summary>
        public long CurrentTransferSize {
            get => _currentTransferSize;
            set {
                _currentTransferSize = value;
                Log.Trace("CurrentTransferSize was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Total size of files of the running transfer that are already fully transferred.
        /// </summary>
        public long TransferredBytesCompleted {
            get => _transferredBytesCompleted;
            set {
                _transferredBytesCompleted = value;
                Log.Trace("TransferredBytesCompleted was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Total size of files of the running transfer that are already fully transferred.
        /// </summary>
        public long TransferredBytesCurrentFile {
            get => _transferredBytesCurrentFile;
            set {
                _transferredBytesCurrentFile = value;
                Log.Trace("TransferredBytesCurrentFile was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// The progress of the current transfer in percent.
        /// </summary>
        public int CurrentTransferPercent {
            get => _currentTransferPercent;
            set {
                _currentTransferPercent = value;
                Log.Trace("CurrentTransferPercent was updated ({0}).", value);
                Serialize();
            }
        }

        #endregion getter / setter methods


        #region getter only methods

        /// <summary>
        /// The full path of the current transfer's temp directory on the target storage.
        /// </summary>
        /// <returns>A string with the path to the current tmp dir.</returns>
        public string TxTargetTmp =>
            Path.Combine(_config.DestinationDirectory,
                _txTargetUser,
                _config.TmpTransferDir,
                Environment.MachineName);

        #endregion getter only methods


        /// <summary>
        /// Helper to set the service state, logging a message if the state has changed.
        /// </summary>
        /// <param name="suspended">The target state for <see cref="ServiceSuspended"/>.</param>
        /// <param name="description">The description to use for the log message as well as for
        /// setting <see cref="StatusDescription"/>.</param>
        public void SetSuspended(bool suspended, string description) {
            if (description == _statusDescription && suspended == _serviceSuspended)
                return;

            _serviceSuspended = suspended;
            _statusDescription = description;
            Serialize();

            if (suspended) {
                Log.Info("Service suspended. Reason(s): [{0}]", description);
            } else {
                Log.Info("Service resuming operation ({0}).", description);
            }
        }
        

        #region validate and report

        /// <summary>
        /// Validate the status and reset attributes with invalid values.
        /// </summary>
        private static void ValidateStatus(ServiceStatus s) {
            // CurrentTransferSrc
            if (s.CurrentTransferSrc.Length > 0
                && !Directory.Exists(s.CurrentTransferSrc)) {
                ReportInvalidStatus("CurrentTransferSrc", s.CurrentTransferSrc,
                    "invalid transfer source path");
                s.CurrentTransferSrc = "";
            }

            // TxTargetUser
            if (s.TxTargetUser.Length > 0
                && !Directory.Exists(s.TxTargetTmp)) {
                ReportInvalidStatus("CurrentTargetTmpPath", s.TxTargetTmp,
                    "invalid temporary path of an unfinished transfer");
                s.TxTargetUser = "";
            }
        }

        /// <summary>
        /// Print a standardized msg about an invalid status attribute to the log.
        /// </summary>
        private static void ReportInvalidStatus(string attribute, string value, string msg) {
            Log.Warn(">>> Invalid status parameter detected, resetting:\n - <{0}> [{1}] {2}.",
                attribute, value, msg);
        }

        /// <summary>
        /// Generate a human-readable sumary of the current transfer.
        /// </summary>
        /// <returns>A string with details on the transfer.</returns>
        public string Summary() {
            return
                $"CurrentTransferSrc: {CurrentTransferSrc}\n" +
                $"TxTargetUser: {TxTargetUser}\n" +
                $"TransferInProgress: {TransferInProgress}\n" +
                $"CurrentTransferSize: {CurrentTransferSize}\n" +
                $"LastStatusUpdate: {LastStatusUpdate:yyyy-MM-dd HH:mm:ss}" +
                $" ({TimeUtils.HumanSince(LastStatusUpdate)})\n" +
                $"LastStorageNotification: {LastStorageNotification:yyyy-MM-dd HH:mm:ss}" +
                $" ({TimeUtils.HumanSince(LastStorageNotification)})\n" +
                $"LastAdminNotification: {LastAdminNotification:yyyy-MM-dd HH:mm:ss}" +
                $" ({TimeUtils.HumanSince(LastAdminNotification)})\n" +
                $"LastGraceNotification: {LastGraceNotification:yyyy-MM-dd HH:mm:ss}" +
                $" ({TimeUtils.HumanSince(LastGraceNotification)})\n";
        }

        #endregion validate and report
    }
}
