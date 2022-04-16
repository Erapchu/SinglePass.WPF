﻿using PasswordManager.Helpers;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for PopupControl.xaml
    /// </summary>
    public partial class PopupControl : Popup
    {
        public PopupControl()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Split code parts - determine position before showing, but first implement hotkeys
            // Obtain popup handle for placement
            IntPtr handle = ((HwndSource)PresentationSource.FromVisual(Child)).Handle;
            
            // TODO: See ConsoleTest project
            // Get position
            //var hFore = GetForegroundWindow();
            //var idAttach = GetWindowThreadProcessId(hFore, out uint id);
            //var curThreadId = GetCurrentThreadId();
            // To attach to current thread
            //var sa = AttachThreadInput(idAttach, curThreadId, true);
            //var caretPos = WindowsKeyboard.GetCaretPos(out POINT caretPoint);
            //ClientToScreen(hFore, ref caretPoint);
            // To dettach from current thread
            //var sd = AttachThreadInput(idAttach, curThreadId, false);
            //var data = string.Format("X={0}, Y={1}", caretPoint.X, caretPoint.Y);
            
            var inputData = "pasted from popup";

            WindowsClipboard.SetText(inputData);

            INPUT[] inputs = new INPUT[4];

            inputs[0].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = WindowsKeyboard.VK_CONTROL;

            inputs[1].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = WindowsKeyboard.VK_V;

            inputs[2].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = WindowsKeyboard.VK_V;
            inputs[2].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

            inputs[3].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = WindowsKeyboard.VK_CONTROL;
            inputs[3].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

            // Send input simulate Ctrl + V
            var uSent = WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);

            // Deprecated WinAPI methods
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_CONTROL, 0, 0, UIntPtr.Zero);
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_V, 0, 0, UIntPtr.Zero);
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_V, 0, WindowsKeyboard.KEYEVENTF_KEYUP, UIntPtr.Zero);
            //WindowsKeyboard.keybd_event(WindowsKeyboard.VK_CONTROL, 0, WindowsKeyboard.KEYEVENTF_KEYUP, UIntPtr.Zero);

            IsOpen = false;
        }
    }
}
