﻿using SinglePass.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace SinglePass.WPF.Views
{
    /// <summary>
    /// Interaction logic for PopupControl.xaml
    /// </summary>
    public partial class PopupControl : Popup
    {
        private PopupViewModel ViewModel => DataContext as PopupViewModel;
        public IntPtr Handle { get; private set; }
        public IntPtr ForegroundHWND
        {
            get => ViewModel.ForegroundHWND;
            set => ViewModel.ForegroundHWND = value;
        }

        public PopupControl(PopupViewModel popupViewModel)
        {
            InitializeComponent();

            popupViewModel.Accept += PopupViewModel_Accept;
            DataContext = popupViewModel;
        }

        private void PopupViewModel_Accept()
        {
            IsOpen = false;
        }

        private void Popup_Opened(object sender, System.EventArgs e)
        {
            Handle = ((HwndSource)PresentationSource.FromVisual(Child)).Handle;
            ViewModel.LoadedCommand.Execute(null);
        }

        private void Popup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DraggableThumb.RaiseEvent(e);
        }

        private void DraggableThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            HorizontalOffset += e.HorizontalChange;
            VerticalOffset += e.VerticalChange;
        }
    }
}
