using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using ATxCommon;

namespace ATxService
{
    public partial class AutoTx
    {
        /// <summary>
        /// Send an email using the configuration values.
        /// </summary>
        /// <param name="recipient">A full email address OR a valid ActiveDirectory account.</param>
        /// <param name="subject">The subject, might be prefixed with a configurable string.</param>
        /// <param name="body">The email body.</param>
        private void SendEmail(string recipient, string subject, string body) {
            subject = _config.EmailPrefix + subject;
            if (string.IsNullOrEmpty(_config.SmtpHost)) {
                Log.Debug("SendEmail: {0}\n{1}", subject, body);
                return;
            }
            if (!recipient.Contains(@"@")) {
                Log.Debug("Invalid recipient, trying to resolve via AD: {0}", recipient);
                recipient = ActiveDirectory.GetEmailAddress(recipient);
            }
            if (string.IsNullOrWhiteSpace(recipient)) {
                Log.Info("Invalid or empty recipient given, NOT sending email!");
                Log.Debug("SendEmail: {0}\n{1}", subject, body);
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
                Log.Error("Error in SendEmail(): {0}\nInnerException: {1}\nStackTrace: {2}",
                    ex.Message, ex.InnerException, ex.StackTrace);
            }
        }

        /// <summary>
        /// Load an email template from a file and do a search-replace with strings in a list.
        /// 
        /// NOTE: template files are expected to be in a subdirectory of the service executable.
        /// </summary>
        /// <param name="templateName">The file name of the template, without path.</param>
        /// <param name="substitions">A list of string-tuples to be used for the search-replace.</param>
        /// <returns>The template with all patterns replaced by their substitution values.</returns>
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
        /// Wrapper method to send an email and log a message using a format string.
        /// 
        /// TODO: Once logging has stabilized we can probably safely remove this method again!
        /// </summary>
        private void AdminDebugLog(string subject, string format, params object[] list) {
            var msg = string.Format(format, list);
            SendAdminEmail(msg, subject);
            msg = subject + "\n" + msg;
            Log.Error(msg);
        }

        /// <summary>
        /// Send a notification email to the AdminEmailAdress.
        /// </summary>
        /// <param name="body">The email text.</param>
        /// <param name="subject">Optional subject for the email.</param>
        private void SendAdminEmail(string body, string subject = "") {
            if (_config.SendAdminNotification == false)
                return;

            var delta = TimeUtils.MinutesSince(_status.LastAdminNotification);
            if (delta < _config.AdminNotificationDelta) {
                Log.Warn("Suppressed admin email, interval too short ({0} vs. {1}):\n\n{2}\n{3}",
                    TimeUtils.MinutesToHuman(delta),
                    TimeUtils.MinutesToHuman(_config.AdminNotificationDelta), subject, body);
                return;
            }

            if (subject == "") {
                subject = ServiceName +
                          " - " + Environment.MachineName +
                          " - Admin Notification";
            }
            body = "Notification from: " + _config.HostAlias
                   + " (" + Environment.MachineName + ")\n\n"
                   + body;
            Log.Debug("Sending an admin notification email.");
            SendEmail(_config.AdminEmailAdress, subject, body);
            _status.LastAdminNotification = DateTime.Now;

        }

        /// <summary>
        /// Send a notification about low drive space to the admin.
        /// </summary>
        /// <param name="spaceDetails">String describing the drives being low on space.</param>
        private void SendLowSpaceMail(string spaceDetails) {
            if (string.IsNullOrWhiteSpace(spaceDetails))
                return;

            var delta = TimeUtils.MinutesSince(_status.LastStorageNotification);
            if (delta < _config.StorageNotificationDelta) {
                Log.Trace("Only {0} since last low-space-notification, skipping.",
                    TimeUtils.MinutesToHuman(delta));
                return;
            }

            Log.Warn("WARNING: {0}", spaceDetails);
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
                Log.Error("Error loading email template: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Send a notification email to the file owner upon successful transfer.
        /// The recipient address is derived from the global variable CurrentTransferSrc.
        /// </summary>
        private void SendTransferCompletedMail() {
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
                Tuple.Create("FACILITY_USER", ActiveDirectory.GetFullUserName(userDir)),
                Tuple.Create("HOST_ALIAS", _config.HostAlias),
                Tuple.Create("HOST_NAME", Environment.MachineName),
                Tuple.Create("DESTINATION_ALIAS", _config.DestinationAlias),
                Tuple.Create("TRANSFERRED_FILES", transferredFiles),
                Tuple.Create("EMAIL_FROM", _config.EmailFrom)
            };

            try {
                var body = LoadMailTemplate("Transfer-Success.txt", substitutions);
                SendEmail(userDir, ServiceName + " - Transfer Notification", body);
                Log.Debug("Sent transfer completed notification to {0}", userDir);
            }
            catch (Exception ex) {
                Log.Error("Error loading email template: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Send a notification email when a transfer has been interrupted before completion.
        /// Recipient address is derived from the global variable CurrentTransferSrc.
        /// </summary>
        private void SendTransferInterruptedMail() {
            if (_config.SendTransferNotification == false)
                return;

            var userDir = new DirectoryInfo(_status.CurrentTransferSrc).Name;

            var substitutions = new List<Tuple<string, string>> {
                Tuple.Create("FACILITY_USER", ActiveDirectory.GetFullUserName(userDir)),
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
                Log.Error("Error loading email template: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Send a report on expired folders in the grace location if applicable.
        /// 
        /// Create a summary of expired folders and send it to the admin address
        /// if the configured GraceNotificationDelta has passed since the last email.
        /// </summary>
        /// <param name="threshold">The number of days used as expiration threshold.</param>
        /// <returns>The summary report, empty if no expired folders exist.</returns>
        private string SendGraceLocationSummary(int threshold) {
            var report = FsUtils.GraceLocationSummary(
                new DirectoryInfo(_config.DonePath), threshold);
            if (string.IsNullOrEmpty(report))
                return "";

            var delta = TimeUtils.MinutesSince(_status.LastGraceNotification);
            report += "\nTime since last grace notification: " +
                TimeUtils.MinutesToHuman(delta) + "\n";
            if (delta < _config.GraceNotificationDelta)
                return report;

            _status.LastGraceNotification = DateTime.Now;
            SendAdminEmail(report, "Grace location cleanup required.");
            return report + "\nNotification sent to AdminEmailAdress.\n";
        }
    }
}