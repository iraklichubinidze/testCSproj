using CommunityToolkit.WinUI.Notifications;

namespace NotificationChecker
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
            {
                Environment.Exit(0);
            }
         
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}