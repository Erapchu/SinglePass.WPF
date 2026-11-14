using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SinglePass.WPF.Helpers
{
    internal sealed class VaultAutoLock : IDisposable
    {
        private const int SessionChange = 0x02B1;
        private const int SessionLock = 0x7;
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

        private readonly HwndSource _source;
        private readonly Action _lockVault;
        private readonly DispatcherTimer _timer;
        private readonly bool _registered;
        private bool _idle;

        internal VaultAutoLock(HwndSource source, Action lockVault)
        {
            _source = source;
            _lockVault = lockVault;
            source.AddHook(WindowProc);
            _registered = WTSRegisterSessionNotification(source.Handle, 0);
            if (!_registered)
                Trace.TraceError("Could not register for Windows session notifications: {0}", Marshal.GetLastWin32Error());

            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += CheckIdle;
            _timer.Start();
        }

        private void CheckIdle(object sender, EventArgs e)
        {
            var input = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref input))
                return;

            var idle = unchecked((uint)Environment.TickCount - input.Tick) >= IdleTimeout.TotalMilliseconds;
            var expired = idle && !_idle;
            _idle = idle;
            if (expired)
                _lockVault();
        }

        private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == SessionChange && wParam.ToInt64() == SessionLock)
                _source.Dispatcher.InvokeAsync(_lockVault, DispatcherPriority.Send);

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= CheckIdle;
            if (_registered)
                WTSUnRegisterSessionNotification(_source.Handle);
            _source.RemoveHook(WindowProc);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            internal uint Size;
            internal uint Tick;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LastInputInfo input);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSRegisterSessionNotification(IntPtr hwnd, uint flags);

        [DllImport("wtsapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
    }
}
