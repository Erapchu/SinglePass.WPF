using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PasswordManager.ViewModels
{
    public class NewCredentialsViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<NewCredentialsViewModel> _lazy = new(GetDesignTimeVM);
        public static NewCredentialsViewModel DesignTimeInstance => _lazy.Value;

        private static NewCredentialsViewModel GetDesignTimeVM()
        {
            var vm = new NewCredentialsViewModel();
            return vm;
        }
        #endregion

        public ObservableCollection<PassFieldViewModel> Fields { get; }

        public NewCredentialsViewModel()
        {
            var defaultFields = new List<PassFieldViewModel>()
            {
                new PassFieldViewModel(new Models.PassField() { Name = "Name", IconKind = PackIconKind.Information }),
                new PassFieldViewModel(new Models.PassField() { Name = "Login", IconKind = PackIconKind.Account }),
                new PassFieldViewModel(new Models.PassField() { Name = "Password", IconKind = PackIconKind.Key }),
                new PassFieldViewModel(new Models.PassField() { Name = "Other", IconKind = PackIconKind.InformationOutline }),
            };
            Fields = new ObservableCollection<PassFieldViewModel>(defaultFields);
        }
    }
}
