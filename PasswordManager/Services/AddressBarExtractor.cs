using PasswordManager.Helpers;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Automation;

namespace PasswordManager.Services
{
    public class AddressBarExtractor
    {
        private readonly ConcurrentDictionary<string, AutomationElement> _automationElementCache = new();

        public AddressBarExtractor()
        {

        }

        // https://stackoverflow.com/questions/18897070/getting-the-current-tabs-url-from-google-chrome-using-c-sharp
        /// <summary>
        /// Extract browsers address bar string
        /// </summary>
        /// <param name="hwnd">Chromium based or Win32 window handle.</param>
        /// <returns>Address bar string</returns>
        public string ExtractAddressBar(IntPtr hwnd)
        {
            _ = WinApiProvider.GetWindowThreadProcessId(hwnd, out uint processId);
            var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;

            for (int i = 0; i < 2; i++)
            {
                try
                {
                    if (!_automationElementCache.TryGetValue(processName, out AutomationElement addressBarElement))
                    {
                        var mainHwnd = process.MainWindowHandle;
                        if (mainHwnd == IntPtr.Zero)
                            return null;

                        AutomationElement element = AutomationElement.FromHandle(mainHwnd);
                        if (element is null)
                            return null;

                        Condition conditions = new AndCondition(
                            new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
                            new PropertyCondition(AutomationElement.IsControlElementProperty, true),
                            new PropertyCondition(AutomationElement.IsContentElementProperty, true),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

                        addressBarElement = element.FindFirst(TreeScope.Descendants, conditions);
                        _automationElementCache.TryAdd(processName, addressBarElement);
                    }

                    return ((ValuePattern)addressBarElement.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
                }
                catch (ElementNotAvailableException)
                {
                    _automationElementCache.TryRemove(processName, out _);
                }
            }

            return null;
        }
    }
}
