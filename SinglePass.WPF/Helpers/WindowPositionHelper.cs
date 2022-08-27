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
    }
}
