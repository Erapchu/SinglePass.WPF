using static Erapkey.Helpers.WinApiProvider;

namespace Erapkey.Enums
{
    internal enum CustomWindowsMessages : uint
    {
        /// <summary>
        /// Shows main window.
        /// </summary>
        WM_SHOW_MAIN_WINDOW = SystemWindowsMessages.WM_USER + 1,
    }
}
