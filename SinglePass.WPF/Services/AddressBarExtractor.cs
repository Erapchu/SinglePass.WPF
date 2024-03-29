﻿using Microsoft.Extensions.Logging;
using SinglePass.WPF.Helpers;
using System;
using System.Collections.Concurrent;
using System.Windows.Automation;

namespace SinglePass.WPF.Services
{
    public class AddressBarExtractor
    {
        private readonly ConcurrentDictionary<IntPtr, AutomationElement> _automationElementCache = new();
        private readonly ILogger<AddressBarExtractor> _logger;

        public AddressBarExtractor(ILogger<AddressBarExtractor> logger)
        {
            _logger = logger;
        }

        // https://stackoverflow.com/questions/18897070/getting-the-current-tabs-url-from-google-chrome-using-c-sharp
        /// <summary>
        /// Extract browsers address bar string
        /// </summary>
        /// <param name="hwnd">Chromium based or Win32 window handle.</param>
        /// <returns>Address bar string</returns>
        public string ExtractAddressBar(IntPtr hwnd)
        {
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        if (!_automationElementCache.TryGetValue(hwnd, out AutomationElement addressBarElement))
                        {
                            AutomationElement element = AutomationElement.FromHandle(hwnd);
                            if (element is null)
                                return null;

                            _ = WinApiProvider.GetWindowThreadProcessId(hwnd, out uint processId);
                            var processIdTrans = (int)processId;

                            Condition conditions = new AndCondition(
                                new PropertyCondition(AutomationElement.ProcessIdProperty, processIdTrans),
                                new PropertyCondition(AutomationElement.IsControlElementProperty, true),
                                new PropertyCondition(AutomationElement.IsContentElementProperty, true),
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

                            addressBarElement = element.FindFirst(TreeScope.Descendants, conditions);
                            _automationElementCache.TryAdd(hwnd, addressBarElement);
                        }

                        return ((ValuePattern)addressBarElement.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
                    }
                    catch (ElementNotAvailableException)
                    {
                        _automationElementCache.TryRemove(hwnd, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }

            return null;
        }
    }
}
