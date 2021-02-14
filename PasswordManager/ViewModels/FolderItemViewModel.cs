using MaterialDesignThemes.Wpf;
using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class FolderItemViewModel : ViewModelBase
    {
        public Folder Model { get; }

        public string Name
        {
            get => Model.Name;
            set
            {
                if (Model.Name == value)
                    return;

                Model.Name = value;
                RaisePropertyChanged();
            }
        }

        public PackIconKind IconKind
        {
            get => Model.Icon;
            set
            {
                if (Model.Icon == value)
                    return;

                Model.Icon = value;
                RaisePropertyChanged();
            }
        }

        public FolderItemViewModel(Folder folder)
        {
            Model = folder ?? throw new ArgumentNullException(nameof(folder));
        }
    }
}
