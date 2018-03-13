using System;
using System.DirectoryServices.AccountManagement;
using NLog;

namespace ATxCommon
{
    public static class ActiveDirectory
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

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
            return username;
        }
    }
}
