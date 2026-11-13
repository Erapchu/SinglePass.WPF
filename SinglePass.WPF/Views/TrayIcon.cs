using Microsoft.Extensions.Logging;
using SinglePass.WPF.Extensions;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Localization;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using static SinglePass.WPF.Helpers.WinApiProvider;

namespace SinglePass.WPF.Views
{
    internal sealed class TrayIcon : IDisposable
    {
        private const int NotifyCallback = 0x8001; // WM_APP + 1
        private const int ContextMenuNotification = 0x007B;
        private const int LeftButtonUp = 0x0202;
        private const int RightButtonUp = 0x0205;
        private const int Select = 0x0400;
        private const int KeySelect = 0x0401;

        private const uint Add = 0;
        private const uint Delete = 2;
        private const uint SetFocus = 3;
        private const uint SetVersion = 4;
        private const uint Version4 = 4;
        private const uint MessageFlag = 0x01;
        private const uint IconFlag = 0x02;
        private const uint TipFlag = 0x04;
        private const uint ShowTipFlag = 0x80;
        private const int SmallIconWidth = 49;
        private const int SmallIconHeight = 50;

        private readonly HwndSource _source;
        private readonly Window _mainWindow;
        private readonly ILogger<TrayIcon> _logger;
        private readonly Icon _icon;
        private readonly uint _taskbarCreated;
        private NotifyIconData _data;
        private ContextMenu _menu;
        private bool _returnFocusToTray;
        private bool _version4;
        private bool _disposed;

        public TrayIcon(HwndSource source, Window mainWindow, ILogger<TrayIcon> logger)
        {
            _source = source;
            _mainWindow = mainWindow;
            _logger = logger;
            _taskbarCreated = RegisterWindowMessage("TaskbarCreated");

            var iconUri = new Uri("/SinglePass.WPF;component/Resources/pass-man.ico", UriKind.Relative);
            using var iconStream = Application.GetResourceStream(iconUri).Stream;
            _icon = new Icon(iconStream, GetSystemMetrics(SmallIconWidth), GetSystemMetrics(SmallIconHeight));
            _data = new NotifyIconData
            {
                Size = (uint)Marshal.SizeOf<NotifyIconData>(),
                Window = source.Handle,
                Id = 1,
                Flags = MessageFlag | IconFlag | TipFlag | ShowTipFlag,
                CallbackMessage = NotifyCallback,
                Icon = _icon.Handle,
                Tip = Constants.AppName,
                Version = Version4
            };

            source.AddHook(WindowProc);
            RegisterIcon();
        }

        private void RegisterIcon()
        {
            if (!ShellNotifyIcon(Add, ref _data))
            {
                _logger.LogWarning("Could not add the SinglePass notification area icon.");
                return;
            }

            _version4 = ShellNotifyIcon(SetVersion, ref _data);
        }

        private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == _taskbarCreated && _taskbarCreated != 0)
                RegisterIcon();

            if (message != NotifyCallback)
                return IntPtr.Zero;

            handled = true;
            // Version 4 puts the notification in the low word of lParam.
            var notification = unchecked((ushort)lParam.ToInt64());
            if (_version4)
            {
                if (notification is Select or KeySelect)
                    _mainWindow.BringToFrontAndActivate();
                else if (notification == ContextMenuNotification)
                    ShowMenu(GetMenuPosition(wParam));
            }
            else if (notification == LeftButtonUp)
            {
                _mainWindow.BringToFrontAndActivate();
            }
            else if (notification == RightButtonUp && GetCursorPos(out var cursor))
            {
                ShowMenu(cursor);
            }

            return IntPtr.Zero;
        }

        private POINT GetMenuPosition(IntPtr position)
        {
            var packed = position.ToInt64();
            var point = new POINT(unchecked((short)packed), unchecked((short)(packed >> 16)));
            if (point.X != -1 || point.Y != -1)
                return point;

            var icon = new NotifyIconIdentifier
            {
                Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                Window = _data.Window,
                Id = _data.Id
            };
            if (ShellNotifyIconGetRect(ref icon, out var bounds) == 0)
                return new POINT((bounds.left + bounds.right) / 2, (bounds.top + bounds.bottom) / 2);

            GetCursorPos(out point);
            return point;
        }

        private void ShowMenu(POINT point)
        {
            _menu ??= CreateMenu();
            var position = _source.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(point.X, point.Y));
            _menu.HorizontalOffset = position.X;
            _menu.VerticalOffset = position.Y;

            _returnFocusToTray = false;
            _menu.IsOpen = true;
            if (PresentationSource.FromVisual(_menu) is HwndSource popupSource)
                SetForegroundWindow(popupSource.Handle);
            _menu.Focus();
        }

        private ContextMenu CreateMenu()
        {
            var menu = new ContextMenu { Placement = PlacementMode.AbsolutePoint };
            var exit = new MenuItem();
            exit.SetBinding(MenuItem.HeaderProperty, new Binding("[Exit]") { Source = LocalizationManager.Instance });
            exit.Click += (_, _) => Application.Current.Shutdown();
            menu.Items.Add(exit);

            menu.PreviewKeyDown += (_, e) => _returnFocusToTray = e.Key == Key.Escape;
            menu.Closed += (_, _) =>
            {
                if (!_disposed && _returnFocusToTray)
                    ShellNotifyIcon(SetFocus, ref _data);
            };

            return menu;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_menu is not null)
                _menu.IsOpen = false;
            ShellNotifyIcon(Delete, ref _data);
            _source.RemoveHook(WindowProc);
            _icon.Dispose();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public uint Size;
            public IntPtr Window;
            public uint Id;
            public uint Flags;
            public uint CallbackMessage;
            public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State;
            public uint StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
            public uint InfoFlags;
            public Guid Guid;
            public IntPtr BalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NotifyIconIdentifier
        {
            public uint Size;
            public IntPtr Window;
            public uint Id;
            public Guid Guid;
        }

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
        private static extern int ShellNotifyIconGetRect(ref NotifyIconIdentifier identifier, out RECT bounds);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT point);
    }
}
