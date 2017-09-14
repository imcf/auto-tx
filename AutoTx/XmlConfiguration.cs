﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;

namespace AutoTx
{
    /// <summary>
    /// Helper class for the nested SpaceMonitoring sections.
    /// </summary>
    public class DriveToCheck
    {
        [XmlElement("DriveName")]
        public string DriveName { get; set; }

        // the value is to be compared to System.IO.DriveInfo.TotalFreeSpace
        // hence we use the same type (long) to avoid unnecessary casts later:
        [XmlElement("SpaceThreshold")]
        public long SpaceThreshold { get; set; }
    }


    /// <summary>
    /// configuration class based on xml
    /// </summary>
    [Serializable]
    public class XmlConfiguration
    {
        public string HostAlias { get; set; }
        public string DestinationAlias { get; set; }
        public string SourceDrive { get; set; }
        public string IncomingDirectory { get; set; }
        public string MarkerFile { get; set; }
        public string ManagedDirectory { get; set; }
        public string DestinationDirectory { get; set; }
        public string TmpTransferDir { get; set; }
        public string SmtpHost { get; set; }
        public string SmtpUserCredential { get; set; }
        public string SmtpPasswortCredential { get; set; }
        public string EmailFrom { get; set; }
        public string AdminEmailAdress { get; set; }
        public string EmailPrefix { get; set; }

        public int ServiceTimer { get; set; }
        public int InterPacketGap { get; set; }
        public int MaxCpuUsage { get; set; }
        public int MinAvailableMemory { get; set; }
        public int SmtpPort { get; set; }
        public int AdminNotificationDelta { get; set; }
        public int StorageNotificationDelta { get; set; }

        public bool SendAdminNotification { get; set; }
        public bool SendTransferNotification { get; set; }
        public bool Debug { get; set; }


        [XmlArray]
        [XmlArrayItem(ElementName = "DriveToCheck")]
        public List<DriveToCheck> SpaceMonitoring { get; set; }

        [XmlArray]
        [XmlArrayItem(ElementName = "ProcessName")]
        public List<string> BlacklistedProcesses { get; set; }

        public static void Serialize(string file, XmlConfiguration c) {
            // the config is never meant to be written by us, therefore:
            throw new SettingsPropertyIsReadOnlyException("The config file should not be written by the service!");
        }

        public static XmlConfiguration Deserialize(string file) {
            var xs = new XmlSerializer(typeof(XmlConfiguration));
            var reader = File.OpenText(file);
            var config = (XmlConfiguration) xs.Deserialize(reader);
            reader.Close();
            return config;
        }

    }


    [Serializable]
    public class XmlStatus
    {
        [NonSerialized] string _storageFile; // remember where we came from
        
        private DateTime _lastStatusUpdate;
        private DateTime _lastStorageNotification;
        private DateTime _lastAdminNotification;

        string _currentTransferSrc;
        string _currentTargetTmp;

        bool _filecopyFinished;
        private bool _cleanShutdown;

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

        public bool FilecopyFinished {
            get { return _filecopyFinished; }
            set {
                _filecopyFinished = value;
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

        public XmlStatus() {
            _currentTransferSrc = "";
            _currentTargetTmp = "";
            _filecopyFinished = true;
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


        public static XmlStatus Deserialize(string file) {
            XmlStatus status;
            var xs = new XmlSerializer(typeof(XmlStatus));
            try {
                var reader = File.OpenText(file);
                status = (XmlStatus) xs.Deserialize(reader);
                reader.Close();
            }
            catch (Exception) {
                // if reading the status XML fails, we return an empty (new) one
                status = new XmlStatus();
            }
            // now set the storage filename:
            status._storageFile = file;
            return status;
        }
    }
}
