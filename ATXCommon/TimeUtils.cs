using System;

namespace ATXCommon
{
    public static class TimeUtils
    {
        /// <summary>
        /// Helper method to create timestamp strings in a consistent fashion.
        /// </summary>
        /// <returns>A timestamp string of the current time.</returns>
        public static string Timestamp() {
            return DateTime.Now.ToString("yyyy-MM-dd__HH-mm-ss");
        }

        /// <summary>
        /// Calculate the time delta since the given date in minutes.
        /// </summary>
        /// <param name="refDate">The reference DateTime to check.</param>
        /// <returns>The number of minutes between the reference date and now.</returns>
        public static int MinutesSince(DateTime refDate) {
            return (int)(DateTime.Now - refDate).TotalMinutes;
        }

        /// <summary>
        /// Calculate the time delta since the given date in seconds.
        /// </summary>
        /// <param name="refDate">The reference DateTime to check.</param>
        /// <returns>The number of seconds between the reference date and now.</returns>
        public static int SecondsSince(DateTime refDate) {
            return (int)(DateTime.Now - refDate).TotalSeconds;
        }
    }
}
