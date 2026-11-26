using Newtonsoft.Json;
using Erapkey.Desktop.Settings;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace Erapkey.Desktop.Hotkeys
{
    public class Hotkey : ICloneable
    {
        #region Static properties

        /// <summary>
        /// A new empty hotkey. Not a shared instance: <see cref="Hotkey"/> is mutable, and Newtonsoft fills an existing
        /// <see cref="AppSettings.ShowPopupHotkey"/> object instead of replacing it, so a shared one would take the saved keys.
        /// </summary>
        public static Hotkey Empty => new() { Key = Key.None, Modifiers = ModifierKeys.None };

        #endregion

        #region Properties

        public ModifierKeys Modifiers { get; set; }
        public Key Key { get; set; }

        [JsonIgnore]
        public bool IsEmpty => Key == Key.None;

        #endregion

        #region User32 pinvokes

        [DllImport("user32.dll")]
        private static extern int MapVirtualKey(uint uCode, uint uMapType);

        #endregion

        #region Static functions

        public static string KeyToString(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
            {
                return new string(new char[] { (char)('0' + (char)(key - Key.D0)) });
            }
            else if (key == Key.LWin)
            {
                return "Win (Left)";
            }
            else if (key == Key.RWin)
            {
                return "Win (Right)";
            }

            string keyStr = key.ToString();

            if (keyStr.StartsWith("Oem"))
            {
                var formsKey = KeyInterop.VirtualKeyFromKey(key);

                int nonVirtualKey = MapVirtualKey((uint)formsKey, 2);
                char mappedChar = Convert.ToChar(nonVirtualKey);

                return "" + mappedChar;
            }

            keyStr = Regex.Replace(keyStr, @"(?<=[A-Za-z])(?=[A-Z][a-z])|(?<=[a-z0-9])(?=[0-9]?[A-Z])", " ");

            return keyStr;
        }

        #endregion

        #region ToString overrides

        public override string ToString()
        {
            try
            {
                string hotkeyString = "";
                if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    hotkeyString += "Ctrl";
                }

                if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    hotkeyString += hotkeyString.Length == 0 ? "Alt" : " + Alt";
                }

                if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    hotkeyString += hotkeyString.Length == 0 ? "Shift" : " + Shift";
                }

                hotkeyString += (hotkeyString.Length == 0 ? "" : " + ") + KeyToString(Key);
                return hotkeyString;
            }
            catch (Exception)
            {
                return base.ToString();
            }
        }

        #endregion

        #region Equals overrides

        public override bool Equals(object obj)
        {
            return obj is not null && obj is Hotkey other
                && Key == other.Key
                && Modifiers == other.Modifiers;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Modifiers, Key);
        }

        #endregion

        #region ICloneable implementation

        object ICloneable.Clone()
        {
            return new Hotkey()
            {
                Key = Key,
                Modifiers = Modifiers,
            };
        }

        public Hotkey Clone()
        {
            return (this as ICloneable).Clone() as Hotkey;
        }

        #endregion
    }
}
