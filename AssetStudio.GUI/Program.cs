using System;
using System.Windows.Forms;

namespace AssetStudio.GUI
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm();
            form.FormClosing += (s, e) =>
            {
                Environment.Exit(0);
            };
            Application.Run(form);
        }
    }
}