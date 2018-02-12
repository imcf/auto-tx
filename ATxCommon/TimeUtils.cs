using System;

namespace ATxCommon
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
        public static long MinutesSince(DateTime refDate) {
            return (long)(DateTime.Now - refDate).TotalMinutes;
        }

        /// <summary>
        /// Calculate the time delta since the given date in seconds.
        /// </summary>
        /// <param name="refDate">The reference DateTime to check.</param>
        /// <returns>The number of seconds between the reference date and now.</returns>
        public static long SecondsSince(DateTime refDate) {
            return (long)(DateTime.Now - refDate).TotalSeconds;
        }

        /// <summary>
        /// Convert a number of seconds to a human readable string.
        /// </summary>
        /// <param name="delta">The time span in seconds.</param>
        /// <returns>A string describing the duration, e.g. "2 hours 34 minutes".</returns>
        public static string SecondsToHuman(long delta) {
            var desc = "ago";
            if (delta < 0) {
                delta *= -1;
                desc = "in the future";
            }

            const int second = 1;
            const int minute = second * 60;
            const int hour = minute * 60;
            const int day = hour * 24;
            const int week = day * 7;
            const int month = day * 30;
            const int year = day * 365;
            
            if (delta < minute)
                return $"{delta} seconds {desc}";

            if (delta < 2 * minute)
                return $"a minute {desc}";

            if (delta < hour)
                return $"{delta / minute} minutes {desc}";

            if (delta < day) {
                var hours = delta / hour;
                var mins = (delta - hours * hour) / minute;
                if (mins > 0)
                    return $"{hours} hours {mins} minutes {desc}";
                return $"{hours} hours {desc}";
            }

            if (delta < 2 * week)
                return $"{delta / day} days ${desc}";

            if (delta < 2 * month)
                return $"{delta / week} weeks {desc}";

            if (delta < year) {
                var months = delta / month;
                var weeks = (delta - months * month) / week;
                var days = ((delta - months * month) - weeks * week) / day;
                var ret = $"{months} months";
                if (weeks > 0)
                    ret += $" {weeks} weeks";
                if (days > 0)
                    ret += $" {days} days";
                return $"{ret} {desc}";
            }

            if (delta < 10 * year) {
                var years = delta / year;
                var months = (delta - years * year) / month;
                var days = ((delta - years * year) - months * month) / day;
                var ret = $"{years} years";
                if (months > 0)
                    ret += $" {months} months";
                if (days > 0)
                    ret += $" {days} days";
                return $"{ret} {desc}";
            }

            return $"{delta / year} years {desc}";
        }

        /// <summary>
        /// Wrapper to use <see cref="SecondsToHuman"/> with minutes as input.
        /// </summary>
        /// <param name="delta">The time span in minutes.</param>
        /// <returns>A string describing the duration, e.g. "2 hours 34 minutes".</returns>
        public static string MinutesToHuman(long delta) {
            return SecondsToHuman(delta * 60);
        }

        /// <summary>
        /// Wrapper to use <see cref="SecondsToHuman"/> with days as input.
        /// </summary>
        /// <param name="delta">The time span in days.</param>
        /// <returns>A string describing the duration, e.g. "12 days" or "3 weeks".</returns>
        public static string DaysToHuman(long delta) {
            return MinutesToHuman(delta * 60 * 24);
        }

        /// <summary>
        /// Wrapper to convert a date into a human readable string relative to now.
        /// </summary>
        /// <param name="refDate">The reference DateTime to check.</param>
        /// <returns>A string describing the delta, e.g. "12 days" or "3 weeks".</returns>
        public static string HumanSince(DateTime refDate) {
            if (refDate == DateTime.MinValue) {
                return "never, reference is DateTime.MinValue";
            }
            return SecondsToHuman(SecondsSince(refDate));
        }
    }
}
