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
    }
}
