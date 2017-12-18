using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;

namespace AutoTx
{
    public partial class AutoTx
    {
        /// <summary>
        /// Send an email using the configuration values.
        /// </summary>
        /// <param name="recipient">A full email address OR a valid ActiveDirectory account.</param>
        /// <param name="subject">The subject, might be prefixed with a configurable string.</param>
        /// <param name="body">The email body.</param>
        public void SendEmail(string recipient, string subject, string body) {
            subject = _config.EmailPrefix + subject;
            if (string.IsNullOrEmpty(_config.SmtpHost)) {
                writeLogDebug("SendEmail: " + subject + "\n" + body);
                return;
            }
            if (!recipient.Contains(@"@")) {
                writeLogDebug("Invalid recipient, trying to resolve via AD: " + recipient);
                recipient = GetEmailAddress(recipient);
            }
            if (string.IsNullOrWhiteSpace(recipient)) {
                writeLogDebug("Invalid or empty recipient given, NOT sending email!");
                writeLogDebug("SendEmail: " + subject + "\n" + body);
                return;
            }
            try {
                var smtpClient = new SmtpClient() {
                    Port = _config.SmtpPort,
                    Host = _config.SmtpHost,
                    EnableSsl = true,
                    Timeout = 10000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new System.Net.NetworkCredential(_config.SmtpUserCredential,
                        _config.SmtpPasswortCredential)
                };
                var mail = new MailMessage(_config.EmailFrom, recipient, subject, body) {
                    BodyEncoding = Encoding.UTF8
                };
                smtpClient.Send(mail);
            }
            catch (Exception ex) {
                writeLog("Error in SendEmail(): " + ex.Message + "\n" +
                         "InnerException: " + ex.InnerException + "\n" +
                         "StackTrace: " + ex.StackTrace);
            }
        }

        /// <summary>
        /// Load an email template from a file and do a search-replace with strings in a list.
        /// 
        /// NOTE: template files are expected to be in a subdirectory of the service executable.
        /// </summary>
        /// <param name="templateName">The file name of the template, without path.</param>
        /// <param name="substitions">A list of string-tuples to be used for the search-replace.</param>
        /// <returns></returns>
        private static string LoadMailTemplate(string templateName, List<Tuple<string, string>> substitions) {
            var text = File.ReadAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Mail-Templates", templateName));
            foreach (var pair in substitions) {
                text = text.Replace(pair.Item1, pair.Item2);
            }
            return text;
        }

        /// <summary>
        /// Send a notification email to the AdminEmailAdress.
        /// </summary>
        /// <param name="body">The email text.</param>
        /// <param name="subject">Optional subject for the email.</param>
        public void SendAdminEmail(string body, string subject = "") {
            if (_config.SendAdminNotification == false)
                return;

            var delta = DateTime.Now - _status.LastAdminNotification;
            if (delta.Minutes < _config.AdminNotificationDelta)
                return;

            if (subject == "") {
                subject = ServiceName +
                          " - " + Environment.MachineName +
                          " - Admin Notification";
            }
            body = "Notification from: " + _config.HostAlias
                   + " (" + Environment.MachineName + ")\n\n"
                   + body;
            // writeLog("Sending an admin notification email.");
            SendEmail(_config.AdminEmailAdress, subject, body);
            _status.LastAdminNotification = DateTime.Now;

        }

        /// <summary>
        /// Send a notification about low drive space to the admin.
        /// </summary>
        /// <param name="spaceDetails">String describing the drives being low on space.</param>
        public void SendLowSpaceMail(string spaceDetails) {
            var delta = DateTime.Now - _status.LastStorageNotification;
            if (delta.Minutes < _config.StorageNotificationDelta)
                return;

            writeLog("WARNING: " + spaceDetails);
            _status.LastStorageNotification = DateTime.Now;

            var substitutions = new List<Tuple<string, string>> {
                Tuple.Create("SERVICE_NAME", ServiceName),
                Tuple.Create("HOST_ALIAS", _config.HostAlias),
                Tuple.Create("HOST_NAME", Environment.MachineName),
                Tuple.Create("LOW_SPACE_DRIVES", spaceDetails)
            };
            try {
                var body = LoadMailTemplate("DiskSpace-Low.txt", substitutions);
                var subject = "Low Disk Space On " + Environment.MachineName;
                SendEmail(_config.AdminEmailAdress, subject, body);
            }
            catch (Exception ex) {
                writeLog("Error loading email template: " + ex.Message, true);
            }
        }

        /// <summary>
        /// Send a notification email to the file owner upon successful transfer.
        /// The recipient address is derived from the global variable CurrentTransferSrc.
        /// </summary>
        public void SendTransferCompletedMail() {
            if (_config.SendTransferNotification == false)
                return;

            var userDir = new DirectoryInfo(_status.CurrentTransferSrc).Name;

            var transferredFiles = "List of transferred files:\n";
            transferredFiles += string.Join("\n", _transferredFiles.Take(30).ToArray());
            if (_transferredFiles.Count > 30) {
                transferredFiles += "\n\n(list showing the first 30 out of the total number of " +
                                    _transferredFiles.Count + " files)\n";
            }

            var substitutions = new List<Tuple<string, string>> {
                Tuple.Create("FACILITY_USER", GetFullUserName(userDir)),
                Tuple.Create("HOST_ALIAS", _config.HostAlias),
                Tuple.Create("HOST_NAME", Environment.MachineName),
                Tuple.Create("DESTINATION_ALIAS", _config.DestinationAlias),
                Tuple.Create("TRANSFERRED_FILES", transferredFiles),
                Tuple.Create("EMAIL_FROM", _config.EmailFrom)
            };

            try {
                var body = LoadMailTemplate("Transfer-Success.txt", substitutions);
                SendEmail(userDir, ServiceName + " - Transfer Notification", body);
            }
            catch (Exception ex) {
                writeLog("Error loading email template: " + ex.Message, true);
            }
        }

        /// <summary>
        /// Send a notification email when a transfer has been interrupted before completion.
        /// Recipient address is derived from the global variable CurrentTransferSrc.
        /// </summary>
        public void SendTransferInterruptedMail() {
            if (_config.SendTransferNotification == false)
                return;

            var userDir = new DirectoryInfo(_status.CurrentTransferSrc).Name;

            var substitutions = new List<Tuple<string, string>> {
                Tuple.Create("FACILITY_USER", GetFullUserName(userDir)),
                Tuple.Create("HOST_ALIAS", _config.HostAlias),
                Tuple.Create("HOST_NAME", Environment.MachineName),
                Tuple.Create("EMAIL_FROM", _config.EmailFrom)
            };

            try {
                var body = LoadMailTemplate("Transfer-Interrupted.txt", substitutions);
                SendEmail(userDir,
                    ServiceName + " - Transfer Interrupted - " + _config.HostAlias,
                    body);
            }
            catch (Exception ex) {
                writeLog("Error loading email template: " + ex.Message, true);
            }
        }
    }
}