using Erapkey.Desktop.Enums;
using System;
using System.Diagnostics;
using System.Linq;
using Erapkey.Core.Helpers;

namespace Erapkey.Desktop.Helpers
{
    internal static class InterprocessHelper
    {
        public static void ShowMainWindow()
        {
            var currentProcessId = System.Environment.ProcessId;
            // A Debug and a Release instance can run side by side; the window name tells them apart.
            var processes = Process
                .GetProcessesByName(Constants.ProcessName)
                .Where(p => !p.Id.Equals(currentProcessId));
            foreach (var spProcess in processes)
            {
                var processWindowHandles = WinApiProvider.EnumerateProcessWindowHandles(spProcess.Id);
                foreach (var processWindowHandle in processWindowHandles)
                {
                    var windowCaption = WinApiProvider.GetWindowText(processWindowHandle);
                    if (windowCaption.Equals(Constants.InterprocessWindowName))
                    {
                        WinApiProvider.PostMessage(
                            processWindowHandle,
                            (uint)CustomWindowsMessages.WM_SHOW_MAIN_WINDOW,
                            IntPtr.Zero,
                            IntPtr.Zero);
                        return;
                    }
                }
            }
        }
    }
}
