using System.Windows;

namespace PasswordManager.Extensions
{
    public static class WindowExtensions
    {
        /// <summary>
        /// Set show in taskbar to <see langword="true"/>, show, normalize state of the window, activate, set top most and focus.
        /// </summary>
        /// <param name="window">Target window.</param>
        public static void BringToFrontAndActivate(this Window window)
        {
            if (window is null)
                return;

            window.ShowInTaskbar = true;
            window.Show();
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }
    }
}
