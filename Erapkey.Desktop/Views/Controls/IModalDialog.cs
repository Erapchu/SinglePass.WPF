using System;
using System.Windows;

namespace Erapkey.Desktop.Views.Controls
{
    /// <summary>
    /// A card shown centered in the main window by <see cref="ModalHost.ShowDialogAsync"/>.
    /// Implemented by a <see cref="FrameworkElement"/>; the dialog keeps its own result.
    /// </summary>
    internal interface IModalDialog
    {
        /// <summary>Bottom bar with the dialog buttons (use the <c>ModalFooter</c> style).</summary>
        FrameworkElement Footer { get; }

        /// <summary>Element focused when the dialog opens.</summary>
        IInputElement InitialFocus { get; }

        /// <summary>Raised when the dialog is done (accepted or cancelled) and should close.</summary>
        event Action CloseRequested;

        /// <summary>Called on Escape. Raise <see cref="CloseRequested"/>, or ignore it while busy.</summary>
        void Cancel();
    }
}
