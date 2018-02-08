using System;
using System.Windows.Forms;

namespace ATxTray
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Length > 0)
                baseDir = args[0];
            Application.Run(new AutoTxTray(baseDir));
        }
    }
}
