using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace ClipboardTranslator
{
    /// <summary>
    /// Service for showing notification popups when translations are completed
    /// </summary>
    public static class NotificationService
    {
        private static NotifyIcon trayIcon;

        /// <summary>
        /// Initialize the notification service with a reference to the tray icon
        /// </summary>
        public static void Initialize(NotifyIcon trayIconReference)
        {
            trayIcon = trayIconReference;
        }

        /// <summary>
        /// Show a notification popup when translation is completed
        /// This method is kept but will not actually display anything as popup functionality is removed
        /// </summary>
        public static void ShowTranslationCompleted(string from, string to, bool showNotification)
        {
            // This functionality is intentionally disabled
            return;
        }

        /// <summary>
        /// Show a warning notification
        /// </summary>
        public static void ShowWarning(string title, string message)
        {
            if (trayIcon == null)
                return;

            try
            {
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = message;
                trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                trayIcon.ShowBalloonTip(3000);
            }
            catch
            {
                // Ignore errors in showing notification
            }
        }

        /// <summary>
        /// Show an error notification
        /// </summary>
        public static void ShowError(string title, string message)
        {
            if (trayIcon == null)
                return;

            try
            {
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = message;
                trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                trayIcon.ShowBalloonTip(3000);
            }
            catch
            {
                // Ignore errors in showing notification
            }
        }
    }
}