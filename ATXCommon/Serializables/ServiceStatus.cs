using System;
using System.IO;
using System.Xml.Serialization;
using NLog;

namespace ATXCommon.Serializables
{
    [Serializable]
    public class ServiceStatus
    {
        [XmlIgnore] string _storageFile; // remember where we came from
        [XmlIgnore] private ServiceConfig _config;
        [XmlIgnore] public string ValidationWarnings;
        [XmlIgnore] private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private DateTime _lastStatusUpdate;
        private DateTime _lastStorageNotification;
        private DateTime _lastAdminNotification;
        private DateTime _lastGraceNotification;

        private string _limitReason;
        private string _currentTransferSrc;
        private string _currentTargetTmp;

        bool _transferInProgress;
        private bool _serviceSuspended;
        private bool _cleanShutdown;

        private long _currentTransferSize;

        [XmlElement("LastStatusUpdate", DataType = "dateTime")]
        public DateTime LastStatusUpdate {
            get { return _lastStatusUpdate; }
            set { _lastStatusUpdate = value; }
        }

        [XmlElement("LastStorageNotification", DataType = "dateTime")]
        public DateTime LastStorageNotification {
            get { return _lastStorageNotification; }
            set {
                _lastStorageNotification = value;
                Serialize();
            }
        }

        [XmlElement("LastAdminNotification", DataType = "dateTime")]
        public DateTime LastAdminNotification {
            get { return _lastAdminNotification; }
            set {
                _lastAdminNotification = value;
                Serialize();
            }
        }

        [XmlElement("LastGraceNotification", DataType = "dateTime")]
        public DateTime LastGraceNotification {
            get { return _lastGraceNotification; }
            set {
                _lastGraceNotification = value;
                Serialize();
            }
        }

        public string LimitReason {
            get { return _limitReason; }
            set {
                _limitReason = value;
                Log.Trace("LimitReason was updated ({0}).", value);
                Serialize();
            }
        }

        public string CurrentTransferSrc {
            get { return _currentTransferSrc; }
            set {
                _currentTransferSrc = value;
                Log.Trace("CurrentTransferSrc was updated ({0}).", value);
                Serialize();
            }
        }

        public string CurrentTargetTmp {
            get { return _currentTargetTmp; }
            set {
                _currentTargetTmp = value;
                Log.Trace("CurrentTargetTmp was updated ({0}).", value);
                Serialize();
            }
        }

        public bool ServiceSuspended {
            get { return _serviceSuspended; }
            set {
                _serviceSuspended = value;
                Log.Trace("ServiceSuspended was updated ({0}).", value);
                Serialize();
            }
        }

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

        public long CurrentTransferSize {
            get { return _currentTransferSize; }
            set {
                _currentTransferSize = value;
                Log.Trace("CurrentTransferSize was updated ({0}).", value);
                Serialize();
            }
        }

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
            Log.Debug("Trying to deserialize status XML file [{0}].", file);
            ServiceStatus status;

            var xs = new XmlSerializer(typeof(ServiceStatus));
            try {
                var reader = File.OpenText(file);
                status = (ServiceStatus) xs.Deserialize(reader);
                reader.Close();
                Log.Debug("Finished deserializing service status XML file.");
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

        private static void ValidateStatus(ServiceStatus s) {
            // CurrentTransferSrc
            if (s.CurrentTransferSrc.Length > 0
                && !Directory.Exists(s.CurrentTransferSrc)) {
                s.ValidationWarnings += " - found non-existing source path of an unfinished " +
                                        "transfer: " + s.CurrentTransferSrc + "\n";
                s.CurrentTransferSrc = "";
            }

            // CurrentTargetTmp
            var currentTargetTmpPath = Path.Combine(s._config.DestinationDirectory,
                s._config.TmpTransferDir,
                s.CurrentTargetTmp);
            if (s.CurrentTargetTmp.Length > 0
                && !Directory.Exists(currentTargetTmpPath)) {
                s.ValidationWarnings += " - found non-existing temporary path of an " +
                                        "unfinished transfer: " + currentTargetTmpPath + "\n";
                s.CurrentTargetTmp = "";
            }
        }

        public string Summary() {
            return
                "CurrentTransferSrc: " + CurrentTransferSrc + "\n" +
                "CurrentTargetTmp: " + CurrentTargetTmp + "\n" +
                "TransferInProgress: " + TransferInProgress + "\n" +
                "CurrentTransferSize: " + CurrentTransferSize + "\n" +
                "LastStatusUpdate: " +
                LastStatusUpdate.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                "LastStorageNotification: " +
                LastStorageNotification.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                "LastAdminNotification: " +
                LastAdminNotification.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                "LastGraceNotification: " +
                LastGraceNotification.ToString("yyyy-MM-dd HH:mm:ss") + "\n";
        }

        /// <summary>
        /// Helper method to generate the full path of the current temp directory.
        /// </summary>
        /// <returns>A string with the path to the last tmp dir.</returns>
        public string CurrentTargetTmpFull() {
            return Path.Combine(_config.DestinationDirectory,
                _config.TmpTransferDir,
                _currentTargetTmp);
        }
    }
}
