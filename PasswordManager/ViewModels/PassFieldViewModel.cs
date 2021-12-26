using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Models;
using System;

namespace PasswordManager.ViewModels
{
    public class PassFieldViewModel : ObservableRecipient
    {
        public PassField Model { get; }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;

                _name = value;
                OnPropertyChanged();
            }
        }

        public string Value
        {
            get => Model.Value;
            set
            {
                if (Model.Value == value)
                    return;

                Model.Value = value;
                OnPropertyChanged();
            }
        }

        public PackIconKind IconKind
        {
            get => Model.IconKind;
            set
            {
                if (Model.IconKind == value)
                    return;

                Model.IconKind = value;
                OnPropertyChanged();
            }
        }

        public PassFieldViewModel(string name, PassField field)
        {
            _name = name;
            Model = field ?? throw new ArgumentNullException(nameof(field));
        }
    }
}
