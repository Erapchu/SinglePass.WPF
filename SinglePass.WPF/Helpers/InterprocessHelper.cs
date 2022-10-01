using SinglePass.WPF.Enums;
using System;
using System.Diagnostics;
using System.Linq;

namespace SinglePass.WPF.Helpers
{
    internal static class InterprocessHelper
    {
        public static void ShowMainWindow()
        {
            var currentProcessId = System.Environment.ProcessId;
            var processes = Process
                .GetProcessesByName(Constants.ProcessName)
                .Where(p => !p.Id.Equals(currentProcessId))
                .ToList();
            if (processes.Count == 0)
                return;

            var spProcess = processes[0];
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
                    break;
                }
            }
        }
    }
}
