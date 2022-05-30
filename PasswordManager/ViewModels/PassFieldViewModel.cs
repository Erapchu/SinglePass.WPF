using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace PasswordManager.ViewModels
{
    [DebuggerDisplay("{Model}")]
    public class PassFieldViewModel : ObservableValidator
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

        [Required(ErrorMessage = "Field shouldn't be empty.")]
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

        public void ValidateValue()
        {
            ValidateProperty(Value, nameof(Value));
        }
    }
}
