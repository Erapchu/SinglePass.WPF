using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Models;
using System;

namespace PasswordManager.ViewModels
{
    public class PassFieldViewModel : ObservableRecipient
    {
        public PassField Model { get; }

        public string Name
        {
            get => Model.Name;
            set
            {
                if (Model.Name == value)
                    return;

                Model.Name = value;
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

        public PassFieldViewModel(PassField field)
        {
            Model = field ?? throw new ArgumentNullException(nameof(field));
        }
    }
}
