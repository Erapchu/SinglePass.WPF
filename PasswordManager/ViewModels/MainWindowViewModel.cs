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
        public ObservableCollection<CredentialViewModel> Credentials { get; private set; } = new ObservableCollection<CredentialViewModel>();

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

        #region SelectedCredential
        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set
            {
                if (_selectedCredential == value)
                    return;

                _selectedCredential = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        public FolderItemViewModel SettingsFolder { get; }

        public MainWindowViewModel()
        {
            foreach (var folder in LoadFolders())
                Folders.Add(folder);

            foreach (var cred in LoadCredentials())
                Credentials.Add(cred);

            // TODO: Leave as is for now
            SettingsFolder = new FolderItemViewModel(new Folder() { Name = "Edit", Icon = MaterialDesignThemes.Wpf.PackIconKind.Tune });
            SelectedFolder = Folders.FirstOrDefault();
        }

        private IEnumerable<FolderItemViewModel> LoadFolders()
        {
            // TODO: load folders from settings for user
            return new List<FolderItemViewModel> { new FolderItemViewModel(new Folder() { Name = "Test" }) };
        }

        private IEnumerable<CredentialViewModel> LoadCredentials()
        {
            // TODO: load credentials from encrypted file
            return new List<CredentialViewModel> { new CredentialViewModel(new Credential() { Name = "Credential", Login="hello", Password="secret", Other="info" }) };
        }
    }
}
