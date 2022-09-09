using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace SinglePass.WPF.Helpers
{
    public static class WindowPositionHelper
    {
        /// <summary>
        /// Checks window position on any screen.
        /// </summary>
        /// <param name="rect">Rectangle.</param>
        /// <returns>True if any working area of screens contains window adjustments, otherwise false.</returns>
        public static bool CheckIsOnAnyScreen(Rect rect)
        {
            bool result = false;
            try
            {
                var rectangle = new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                result = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rectangle));
                return result;
            }
            catch
            {
                //ErrorHandler.Instance.HandleException(ex);
                return result;
            }
        }

        /// <summary>
        /// Shifts window to nearest screen if it's out of it.
        /// </summary>
        /// <param name="window">Target window.</param>
        public static void ShiftWindowToScreen(Window window)
        {
            var windowPoint = new System.Drawing.Point((int)window.Left, (int)window.Top);
            Screen activeScreen = Screen.FromPoint(windowPoint);

            var windowRight = window.Left + window.Width;
            var screenRight = activeScreen.WorkingArea.X + activeScreen.WorkingArea.Width;
            if (windowRight > screenRight)
            {
                window.Left = screenRight - window.Width;
            }

            var windowBottom = window.Top + window.Height;
            var screenBottom = activeScreen.WorkingArea.Y + activeScreen.WorkingArea.Height;
            if (windowBottom > screenBottom)
            {
                window.Top = screenBottom - window.Height;
            }
        }
    }
}
