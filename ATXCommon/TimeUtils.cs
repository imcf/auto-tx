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

        /// <summary>
        /// Convert a number of seconds to a human readable string.
        /// </summary>
        /// <param name="delta">The time span in seconds.</param>
        /// <returns>A string describing the duration, e.g. "2 hours 34 minutes".</returns>
        public static string SecondsToHuman(int delta) {
            const int second = 1;
            const int minute = second * 60;
            const int hour = minute * 60;
            const int day = hour * 24;
            const int week = day * 7;
            
            if (delta < minute)
                return delta + " seconds";

            if (delta < 2 * minute)
                return "a minute";

            if (delta < hour)
                return delta / minute + " minutes";

            if (delta < day) {
                var hours = delta / hour;
                var mins = (delta - hours * hour) / minute;
                if (mins > 0)
                    return hours + " hours " + mins + " minutes";
                return hours + " hours";
            }

            if (delta < 2 * week)
                return delta / day + " days";

            return delta / week + " weeks";
        }

        /// <summary>
        /// Wrapper to use <see cref="SecondsToHuman"/> with minutes as input.
        /// </summary>
        /// <param name="delta">The time span in minutes.</param>
        /// <returns>A string describing the duration, e.g. "2 hours 34 minutes".</returns>
        public static string MinutesToHuman(int delta) {
            return SecondsToHuman(delta * 60);
        }

        /// <summary>
        /// Wrapper to use <see cref="SecondsToHuman"/> with days as input.
        /// </summary>
        /// <param name="delta">The time span in days.</param>
        /// <returns>A string describing the duration, e.g. "12 days" or "3 weeks".</returns>
        public static string DaysToHuman(int delta) {
            return MinutesToHuman(delta * 60 * 24);
        }
    }
}
