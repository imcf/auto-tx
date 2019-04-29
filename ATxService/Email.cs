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
            subject = $"{_config.EmailPrefix}{ServiceName} - {subject} - {_config.HostAlias}";
            body += $"\n\n--\n[{_versionSummary}]\n";
            if (string.IsNullOrEmpty(_config.SmtpHost)) {
                Log.Debug("SendEmail: config option <SmtpHost> is unset, not sending mail - " +
                          "content shown below.\n[Subject] {0}\n[Body] {1}", subject, body);
                return;
            }
            if (!recipient.Contains(@"@")) {
                Log.Trace("Invalid recipient, trying to resolve via AD: {0}", recipient);
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
        /// Send a notification email to the AdminEmailAddress.
        /// </summary>
        /// <param name="body">The email text.</param>
        /// <param name="subject">Optional subject for the email.</param>
        /// <returns>True in case an email was sent, false otherwise.</returns>
        private bool SendAdminEmail(string body, string subject = "") {
            if (_config.SendAdminNotification == false)
                return false;

            var delta = TimeUtils.MinutesSince(_status.LastAdminNotification);
            if (delta < _config.AdminNotificationDelta) {
                Log.Warn("Suppressed admin email, interval too short ({0} vs. {1}):\n\n{2}\n{3}",
                    TimeUtils.MinutesToHuman(delta),
                    TimeUtils.MinutesToHuman(_config.AdminNotificationDelta), subject, body);
                return false;
            }

            if (string.IsNullOrWhiteSpace(subject))
                subject = "Admin Notification";

            body = $"Notification from: {_config.HostAlias} ({Environment.MachineName})\n\n{body}";
            Log.Debug("Sending an admin notification email.");
            SendEmail(_config.AdminEmailAddress, subject, body);
            _status.LastAdminNotification = DateTime.Now;
            Log.Debug("{0} sent to AdminEmailAddress.", subject);
            return true;
        }

        /// <summary>
        /// Send a notification about low drive space to the admin if the time since the last
        /// notification has elapsed the configured delta. The report will also contain a summary
        /// of the grace location status. If none of the drives are low on space nothing will be
        /// done (i.e. only a generic trace-level message will be logged).
        /// </summary>
        private void SendLowSpaceMail() {
            if (_storage.AllDrivesAboveThreshold()) {
                Log.Trace("Free space on all drives above threshold.");
                return;
            }

            var delta = TimeUtils.MinutesSince(_status.LastStorageNotification);
            if (delta < _config.StorageNotificationDelta) {
                Log.Trace("Last low-space-notification was {0}, skipping.",
                    TimeUtils.MinutesToHuman(delta));
                return;
            }

            // reaching this point means a notification will be sent, so now we can ask for the
            // full storage status report:
            var report = _storage.Summary();

            Log.Warn("WARNING: {0}", report);
            _status.LastStorageNotification = DateTime.Now;

            var substitutions = new List<Tuple<string, string>> {
                Tuple.Create("SERVICE_NAME", ServiceName),
                Tuple.Create("HOST_ALIAS", _config.HostAlias),
                Tuple.Create("HOST_NAME", Environment.MachineName),
                Tuple.Create("LOW_SPACE_DRIVES", report)
            };
            try {
                var body = LoadMailTemplate("DiskSpace-Low.txt", substitutions);
                // explicitly use SendEmail() instead of SendAdminEmail() here to circumvent the
                // additional checks done in the latter one and make sure the low space email is
                // sent out independently of that:
                SendEmail(_config.AdminEmailAddress, "low disk space", body);
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
                transferredFiles += $"\n\n(showing the first 30 files ({_transferredFiles.Count} in total)\n";
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
                SendEmail(userDir, "Transfer Notification", body);
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
                SendEmail(userDir, "INTERRUPTED Transfer", body);
            }
            catch (Exception ex) {
                Log.Error("Error loading email template: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Send a report on expired folders in the grace location if applicable.
        ///
        /// A summary of expired folders is created and sent to the admin address if the configured
        /// GraceNotificationDelta has passed since the last email. The report will also contain a
        /// summary of free disk space for all configured drives.
        /// </summary>
        /// <returns>True if a report was sent, false otherwise (includes situations where there
        /// are expired directories but the report has not been sent via email as the grace
        /// notification delta hasn't expired yet, the report will still be logged then).</returns>
        private bool SendGraceLocationSummary() {
            if (_storage.ExpiredDirsCount == 0)
                return false;

            var report = _storage.Summary() +
                         "\nTime since last grace notification: " +
                         $"{TimeUtils.HumanSince(_status.LastGraceNotification)}\n";

            if (TimeUtils.MinutesSince(_status.LastGraceNotification) < _config.GraceNotificationDelta) {
                Log.Debug(report);
                return false;
            }

            _status.LastGraceNotification = DateTime.Now;
            return SendAdminEmail(report, "grace location summary");
        }

        /// <summary>
        /// Send a system health report if enough time has elapsed since the previous one.
        /// </summary>
        /// <param name="report">The health report.</param>
        /// <returns>True in case the report was sent, false otherwise.</returns>
        private bool SendHealthReport(string report) {
            var elapsedHuman = TimeUtils.HumanSince(_status.LastStartupNotification);

            if (TimeUtils.MinutesSince(_status.LastStartupNotification) < _config.StartupNotificationDelta) {
                Log.Trace("Not sending system health report now, last one has been sent {0}",
                    elapsedHuman);
                return false;
            }

            report += $"\nPrevious system health report notification was sent {elapsedHuman}.\n";
            _status.LastStartupNotification = DateTime.Now;
            // TODO: recipients for the health report should be configurable, defaulting to admin
            return SendAdminEmail(report, "system health report");
        }
    }
}