using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.Views.MessageBox
{
    public enum MaterialMessageBoxButtons
    {
        /// <summary>
        /// The message box contains an OK button.
        /// </summary>
        OK,

        /// <summary>
        /// The message box contains OK and Cancel buttons.
        /// </summary>
        OKCancel,

        /// <summary>
        /// The message box contains Yes and No buttons.
        /// </summary>
        YesNo,

        /// <summary>
        /// The message box contains Yes, No, and Cancel buttons.
        /// </summary>
        YesNoCancel,

        /// <summary>
        /// The message box contains Abort, Retry, and Ignore buttons.
        /// </summary>
        AbortRetryIgnore,

        /// <summary>
        /// The message box contains Retry and Cancel buttons.
        /// </summary>
        RetryCancel
    }
}
