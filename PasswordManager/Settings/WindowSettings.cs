using System;
using System.Diagnostics;
using System.Windows;

namespace PasswordManager.Settings
{
    [DebuggerDisplay("Left: {Left}, Top: {Top}, Width: {Width}, Height: {Height}, State: {WindowState}")]
    public class WindowSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public WindowState WindowState { get; set; }

        public override bool Equals(object obj)
        {
            return obj is WindowSettings settings &&
                   Left == settings.Left &&
                   Top == settings.Top &&
                   Width == settings.Width &&
                   Height == settings.Height &&
                   WindowState == settings.WindowState;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Width, Height, WindowState);
        }
    }
}
