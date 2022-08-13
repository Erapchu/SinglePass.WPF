using System.Windows;

namespace SinglePass.WPF.Helpers
{
    public static class WindowPositionHelper
    {
        public static bool IsOnPrimaryScreen(Rect rect)
        {
            var screenRect = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            return screenRect.IntersectsWith(rect);
        }
    }
}
