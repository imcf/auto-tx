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
        [XmlIgnore] private string _storageFile; // remember where we came from
        [XmlIgnore] private ServiceConfig _config;
        [XmlIgnore] private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private DateTime _lastStatusUpdate;
        private DateTime _lastStorageNotification;
        private DateTime _lastAdminNotification;
        private DateTime _lastGraceNotification;

        private string _limitReason;
        private string _currentTransferSrc;
        private string _currentTargetTmp;

        private bool _transferInProgress;
        private bool _serviceSuspended;
        private bool _cleanShutdown;

        private long _currentTransferSize;


        #region constructor, serializer and deserializer

        /// <summary>
        /// The constructor, setting default values.
        /// </summary>
        public ServiceStatus() {
            _currentTransferSrc = "";
            _currentTargetTmp = "";
            _transferInProgress = false;
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
            get { return _lastStatusUpdate; }
            set { _lastStatusUpdate = value; }
        }

        /// <summary>
        /// Timestamp indicating when the last storage notification has been sent.
        /// </summary>
        [XmlElement("LastStorageNotification", DataType = "dateTime")]
        public DateTime LastStorageNotification {
            get { return _lastStorageNotification; }
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
            get { return _lastAdminNotification; }
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
            get { return _lastGraceNotification; }
            set {
                _lastGraceNotification = value;
                Serialize();
            }
        }

        /// <summary>
        /// String indicating why the service is currently suspended (empty if not suspended).
        /// </summary>
        public string LimitReason {
            get { return _limitReason; }
            set {
                _limitReason = value;
                Log.Trace("LimitReason was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// The full path to the folder currently being transferred.
        /// </summary>
        public string CurrentTransferSrc {
            get { return _currentTransferSrc; }
            set {
                _currentTransferSrc = value;
                Log.Trace("CurrentTransferSrc was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// The name of the temporary folder being used for the currently running transfer,
        /// relative to "DestinationDirectory\TmpTransferDir" (i.e. the target username). See also
        /// <seealso cref="CurrentTargetTmpFull"/> on details for assembling the full path.
        /// </summary>
        public string CurrentTargetTmp {
            get { return _currentTargetTmp; }
            set {
                _currentTargetTmp = value;
                Log.Trace("CurrentTargetTmp was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Flag indicating whether the service is currently suspended.
        /// </summary>
        public bool ServiceSuspended {
            get { return _serviceSuspended; }
            set {
                _serviceSuspended = value;
                Log.Trace("ServiceSuspended was updated ({0}).", value);
                Serialize();
            }
        }

        /// <summary>
        /// Flag indicating whether a transfer is currently running.
        /// </summary>
        public bool TransferInProgress {
            get { return _transferInProgress; }
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
            get { return _cleanShutdown; }
            set {
                _cleanShutdown = value;
                Serialize();
            }
        }

        /// <summary>
        /// The full size of the current transfer in bytes.
        /// </summary>
        public long CurrentTransferSize {
            get { return _currentTransferSize; }
            set {
                _currentTransferSize = value;
                Log.Trace("CurrentTransferSize was updated ({0}).", value);
                Serialize();
            }
        }

        #endregion getter / setter methods
        

        /// <summary>
        /// Helper method to generate the full path of the current temp directory.
        /// </summary>
        /// <returns>A string with the path to the last tmp dir.</returns>
        public string CurrentTargetTmpFull() {
            return Path.Combine(_config.DestinationDirectory,
                _config.TmpTransferDir,
                _currentTargetTmp);
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

            // CurrentTargetTmp
            var currentTargetTmpPath = s.CurrentTargetTmpFull();
            if (s.CurrentTargetTmp.Length > 0
                && !Directory.Exists(currentTargetTmpPath)) {
                ReportInvalidStatus("CurrentTargetTmpPath", currentTargetTmpPath,
                    "invalid temporary path of an unfinished transfer");
                s.CurrentTargetTmp = "";
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
                "CurrentTransferSrc: " + CurrentTransferSrc + "\n" +
                "CurrentTargetTmp: " + CurrentTargetTmp + "\n" +
                "TransferInProgress: " + TransferInProgress + "\n" +
                "CurrentTransferSize: " + CurrentTransferSize + "\n" +
                "LastStatusUpdate: " +
                LastStatusUpdate.ToString("yyyy-MM-dd HH:mm:ss") + " (" +
                TimeUtils.SecondsToHuman(TimeUtils.SecondsSince(LastStatusUpdate)) +
                " ago)\n" +
                "LastStorageNotification: " +
                LastStorageNotification.ToString("yyyy-MM-dd HH:mm:ss") + " (" +
                TimeUtils.SecondsToHuman(TimeUtils.SecondsSince(LastStorageNotification)) +
                " ago)\n" +
                "LastAdminNotification: " +
                LastAdminNotification.ToString("yyyy-MM-dd HH:mm:ss") + " (" +
                TimeUtils.SecondsToHuman(TimeUtils.SecondsSince(LastAdminNotification)) +
                " ago)\n" +
                "LastGraceNotification: " +
                LastGraceNotification.ToString("yyyy-MM-dd HH:mm:ss") + " (" +
                TimeUtils.SecondsToHuman(TimeUtils.SecondsSince(LastGraceNotification)) +
                " ago)\n";
        }

        #endregion validate and report
    }
}
