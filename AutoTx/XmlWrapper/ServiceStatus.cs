using System;
using System.IO;
using System.Xml.Serialization;

namespace AutoTx.XmlWrapper
{
    [Serializable]
    public class ServiceStatus
    {
        [XmlIgnore] string _storageFile; // remember where we came from
        [XmlIgnore] private ServiceConfig _config; 
        [XmlIgnore] public string ValidationWarnings;
        
        private DateTime _lastStatusUpdate;
        private DateTime _lastStorageNotification;
        private DateTime _lastAdminNotification;

        private string _limitReason;
        string _currentTransferSrc;
        string _currentTargetTmp;

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

        public string LimitReason {
            get { return _limitReason; }
            set {
                _limitReason = value;
                log("LimitReason was updated (" + value + "), calling Serialize()...");
                Serialize();
            }
        }
        
        public string CurrentTransferSrc {
            get { return _currentTransferSrc; }
            set {
                _currentTransferSrc = value;
                log("CurrentTransferSrc was updated (" + value + "), calling Serialize()...");
                Serialize();
            }
        }

        public string CurrentTargetTmp {
            get { return _currentTargetTmp; }
            set {
                _currentTargetTmp = value;
                log("CurrentTargetTmp was updated (" + value + "), calling Serialize()...");
                Serialize();
            }
        }

        public bool ServiceSuspended {
            get { return _serviceSuspended; }
            set {
                _serviceSuspended = value;
                log("ServiceSuspended was updated (" + value + "), calling Serialize()...");
                Serialize();
            }
        }

        public bool TransferInProgress {
            get { return _transferInProgress; }
            set {
                _transferInProgress = value;
                log("FilecopyFinished was updated (" + value + "), calling Serialize()...");
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
                log("CurrentTransferSize was updated (" + value + "), calling Serialize()...");
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
                log("File name for XML serialization is not set, doing nothing.");
                return;
            }
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
                log("Error in Serialize(): " + ex.Message);
            }
            log("Finished serializing " + _storageFile);
        }

        static void log(string message) {
            // use Console.WriteLine until proper logging is there (running as a system
            // service means those messages will disappear):
            Console.WriteLine(message);
            /*
            using (var sw = File.AppendText(@"C:\Tools\AutoTx\console.log")) {
                sw.WriteLine(message);
            }
             */
        }

        public static ServiceStatus Deserialize(string file, ServiceConfig config) {
            ServiceStatus status;

            var xs = new XmlSerializer(typeof(ServiceStatus));
            try {
                var reader = File.OpenText(file);
                status = (ServiceStatus) xs.Deserialize(reader);
                reader.Close();
            }
            catch (Exception) {
                // if reading the status XML fails, we return an empty (new) one
                status = new ServiceStatus();
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
                                        "unfinished transfer: " + currentTargetTmpPath+ "\n";
                s.CurrentTargetTmp = "";
            }

        }
    }
}
