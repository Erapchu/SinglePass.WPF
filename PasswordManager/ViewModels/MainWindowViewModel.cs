using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region Design time instance
        private static readonly Lazy<MainWindowViewModel> _lazy = new Lazy<MainWindowViewModel>(() => new MainWindowViewModel());
        public static MainWindowViewModel DesignTimeInstance => _lazy.Value;
        #endregion

        public ObservableCollection<FolderItemViewModel> Folders { get; private set; } = new ObservableCollection<FolderItemViewModel>();

        #region SelectedFolder
        private FolderItemViewModel _selectedFolder;
        public FolderItemViewModel SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (_selectedFolder == value)
                    return;

                _selectedFolder = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        public MainWindowViewModel()
        {
            foreach (var folder in LoadFolders())
                Folders.Add(folder);
        }

        private IEnumerable<FolderItemViewModel> LoadFolders()
        {
            // TODO: load folders from settings for user
            return new List<FolderItemViewModel> { new FolderItemViewModel(new Folder()) };
        }
    }
}
