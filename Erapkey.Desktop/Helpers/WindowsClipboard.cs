using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Erapkey.Desktop.Helpers
{
    /// <summary>
    /// Win32 clipboard writer for secrets: the text is excluded from clipboard history (Win+V),
    /// cloud clipboard sync and clipboard monitors, and is cleared automatically after <see cref="ClearDelay"/>
    /// unless something else has been copied since.
    /// </summary>
    /// <remarks>
    /// Originally based on https://github.com/CopyText/TextCopy/blob/main/src/TextCopy/WindowsClipboard.cs.
    /// Synchronous on purpose: auto-type puts a value on the clipboard and immediately sends Ctrl+V.
    /// </remarks>
    internal static class WindowsClipboard
    {
        public static readonly TimeSpan ClearDelay = TimeSpan.FromSeconds(30);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint GMEM_ZEROINIT = 0x0040;
        private const int OpenAttempts = 10;
        private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(100);

        // https://learn.microsoft.com/windows/win32/dataxchg/clipboard-formats#cloud-clipboard-and-clipboard-history-formats
        private static readonly Lazy<uint> ExcludeFromMonitorFormat = new(() => RegisterFormat("ExcludeClipboardContentFromMonitorProcessing"));
        private static readonly Lazy<uint> CanIncludeInHistoryFormat = new(() => RegisterFormat("CanIncludeInClipboardHistory"));
        private static readonly Lazy<uint> CanUploadToCloudFormat = new(() => RegisterFormat("CanUploadToCloudClipboard"));

        private static readonly object _sync = new();
        private static Timer _clearTimer;
        private static uint _ownedSequenceNumber;

        public static void SetText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            lock (_sync)
            {
                OpenClipboardWithRetry();
                var completed = false;
                try
                {
                    if (!EmptyClipboard())
                        ThrowWin32();

                    var dwordZero = new byte[sizeof(uint)];
                    SetData(ExcludeFromMonitorFormat.Value, dwordZero);
                    SetData(CanIncludeInHistoryFormat.Value, dwordZero);
                    SetData(CanUploadToCloudFormat.Value, dwordZero);

                    var bytes = new byte[(text.Length + 1) * sizeof(char)]; // trailing zeros = null terminator
                    try
                    {
                        System.Text.Encoding.Unicode.GetBytes(text, 0, text.Length, bytes, 0);
                        SetData(CF_UNICODETEXT, bytes);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(bytes);
                    }

                    completed = true;
                }
                finally
                {
                    // Never leave a secret on the clipboard without the privacy markers.
                    if (!completed)
                        EmptyClipboard();
                    CloseClipboard();
                }

                _ownedSequenceNumber = GetClipboardSequenceNumber();
                _clearTimer ??= new Timer(_ => ClearIfOwned());
                _clearTimer.Change(ClearDelay, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Clears the clipboard if it still holds the value set by <see cref="SetText"/>. Best effort, never throws.
        /// </summary>
        public static void ClearIfOwned()
        {
            lock (_sync)
            {
                var owned = _ownedSequenceNumber;
                _ownedSequenceNumber = 0;
                _clearTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                if (owned == 0 || GetClipboardSequenceNumber() != owned)
                    return;

                if (!OpenClipboard(IntPtr.Zero))
                    return;
                try
                {
                    if (GetClipboardSequenceNumber() == owned)
                        EmptyClipboard();
                }
                finally
                {
                    CloseClipboard();
                }
            }
        }

        private static void SetData(uint format, byte[] data)
        {
            var hGlobal = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)data.Length);
            if (hGlobal == IntPtr.Zero)
                ThrowWin32();

            try
            {
                var target = GlobalLock(hGlobal);
                if (target == IntPtr.Zero)
                    ThrowWin32();
                try
                {
                    Marshal.Copy(data, 0, target, data.Length);
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }

                if (SetClipboardData(format, hGlobal) == IntPtr.Zero)
                    ThrowWin32();

                hGlobal = IntPtr.Zero; // the system owns the memory now
            }
            finally
            {
                if (hGlobal != IntPtr.Zero)
                    GlobalFree(hGlobal);
            }
        }

        private static void OpenClipboardWithRetry()
        {
            for (var attempt = 1; !OpenClipboard(IntPtr.Zero); attempt++)
            {
                if (attempt == OpenAttempts)
                    ThrowWin32();

                Thread.Sleep(OpenRetryDelay);
            }
        }

        private static uint RegisterFormat(string name)
        {
            var format = RegisterClipboardFormat(name);
            if (format == 0)
                ThrowWin32();
            return format;
        }

        private static void ThrowWin32()
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr data);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();
    }
}
