using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Management;
using NLog;

namespace ATXCommon
{
    public static class ActiveDirectory
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Check if a user is currently logged into Windows.
        /// 
        /// WARNING: this DOES NOT ACCOUNT for users logged in via RDP!!
        /// </summary>
        /// See https://stackoverflow.com/questions/5218778/ for the RDP problem.
        public static bool NoUserIsLoggedOn() {
            var username = "";
            try {
                var searcher = new ManagementObjectSearcher("SELECT UserName " +
                                                            "FROM Win32_ComputerSystem");
                var collection = searcher.Get();
                username = (string)collection.Cast<ManagementBaseObject>().First()["UserName"];
            }
            catch (Exception ex) {
                // TODO / FIXME: combine log and admin-email!
                var msg = string.Format("Error in getCurrentUsername(): {0}", ex.Message);
                Log.Error(msg);
                // TODO: FIXME!
                // SendAdminEmail(msg);
            }
            return username == "";
        }

        /// <summary>
        /// Get the user email address from ActiveDirectory.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>Email address of AD user, an empty string if not found.</returns>
        public static string GetEmailAddress(string username) {
            try {
                using (var pctx = new PrincipalContext(ContextType.Domain)) {
                    using (var up = UserPrincipal.FindByIdentity(pctx, username)) {
                        if (up != null && !string.IsNullOrWhiteSpace(up.EmailAddress)) {
                            return up.EmailAddress;
                        }
                    }
                }
            }
            catch (Exception ex) {
                Log.Warn("Can't find email address for {0}: {1}", username, ex.Message);
            }
            return "";
        }

        /// <summary>
        /// Get the full user name (human-friendly) from ActiveDirectory.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>A human-friendly string representation of the user principal.</returns>
        public static string GetFullUserName(string username) {
            try {
                using (var pctx = new PrincipalContext(ContextType.Domain)) {
                    using (var up = UserPrincipal.FindByIdentity(pctx, username)) {
                        if (up != null)
                            return up.GivenName + " " + up.Surname;
                    }
                }
            }
            catch (Exception ex) {
                Log.Warn("Can't find full name for {0}: {1}", username, ex.Message);
            }
            return "";
        }
    }
}
