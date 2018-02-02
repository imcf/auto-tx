using System;
using System.ComponentModel;
using NLog;
using NLog.Common;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace ATxCommon.NLog
{
    /// <summary>
    /// A wrapper target for NLog, limiting the rate of messages being logged.
    /// 
    /// Meant to be used in conjunction with the MailTarget class, to avoid flooding the recipient
    /// with too many emails and probably being banned by the SMTP server for spamming.
    /// NOTE: should always be used in combination with another target (FileTarget) to ensure that
    /// all messages are being logged, including those ones discarded by *this* target.
    /// </summary>
    [Target("FrequencyWrapper", IsWrapper = true)]
    public class RateLimitWrapper : WrapperTargetBase
    {
        private DateTime _lastLogEvent = DateTime.MinValue;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        protected override void Write(AsyncLogEventInfo logEvent) {
            if ((DateTime.Now - _lastLogEvent).TotalMinutes >= MinLogInterval) {
                _lastLogEvent = DateTime.Now;
                WrappedTarget.WriteAsyncLogEvent(logEvent);
            } else {
                logEvent.Continuation(null);
                // EXPERIMENTAL: check if the level of the log event is higher than "WARN" (3) and
                // log a debug message about keeping back the email message:
                if (logEvent.LogEvent.Level.Ordinal > 3) {
                    Log.Debug("RateLimitWrapper: not sending email, frequency too high!");
                }
            }
        }

        [DefaultValue(30)]
        public int MinLogInterval { get; set; }
    }
}
