using CommunityToolkit.Mvvm.ComponentModel;
using SinglePass.Core.Models;
using SinglePass.WPF.Services;
using SinglePass.WPF.ViewModels.Entries;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace SinglePass.WPF.ViewModels
{
    /// <summary>
    /// A vault entry: the fields every entry type has, plus <see cref="Data"/> for the type itself.
    /// </summary>
    [DebuggerDisplay("{Model}")]
    public partial class CredentialViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<CredentialViewModel> _lazy = new(GetDesignTimeVM);
        public static CredentialViewModel DesignTimeInstance => _lazy.Value;
        private static CredentialViewModel GetDesignTimeVM()
        {
            var additionalFields = new List<PassField>() { new PassField() { Name = "Design additional field", Value = "Test value" } };
            var model = Credential.CreateNew();
            model.AdditionalFields = additionalFields;

            var vm = new CredentialViewModel(model, null);
            return vm;
        }
        #endregion

        private readonly IFavIconCollector _favIconCollector;

        public Credential Model { get; }

        public CredentialType Type => Model.Type;

        /// <summary>The type-specific part, shown by the view and editor of its type.</summary>
        public CredentialDataViewModel Data { get; }

        /// <summary>Same object as <see cref="Data"/> for logins; null for other types.</summary>
        public LoginViewModel Login => Data as LoginViewModel;

        /// <summary>Same object as <see cref="Data"/> for SSH keys; null for other types.</summary>
        public SshKeyViewModel SshKey => Data as SshKeyViewModel;

        public bool IsLogin => Login != null;

        public PassFieldViewModel NameFieldVM { get; }
        public PassFieldViewModel OtherFieldVM { get; }
        public ObservableCollection<PassFieldViewModel> AdditionalFields { get; }

        public string Subtitle => Data.Subtitle;
        public string SubtitleToolTip => Data.SubtitleToolTip;
        public string Glyph => Data.Glyph;

        // The model keeps UTC; the UI shows and groups by local time.
        public DateTime LastModifiedTime
        {
            get => Model.LastModifiedTime.ToLocalTime();
            set => Model.LastModifiedTime = value;
        }

        public DateTime CreationTime
        {
            get => Model.CreationTime.ToLocalTime();
            set => Model.CreationTime = value;
        }

        [ObservableProperty]
        public partial bool PasswordVisible { get; set; }

        private ImageSource _favIcon;

        /// <summary>Icon of the login's site; null for other entry types and while it loads.</summary>
        public ImageSource FavIcon
        {
            get
            {
                if (_favIconCollector != null && Login?.WebsiteDomain is { } domain)
                    _favIconCollector.ScheduleGetImage(
                        "https://" + domain,
                        image =>
                        {
                            if (Login?.WebsiteDomain == domain)
                                SetProperty(ref _favIcon, image, nameof(FavIcon));
                        });

                return _favIcon;
            }
        }

        public CredentialViewModel(Credential credential, IFavIconCollector favIconCollector)
        {
            Model = credential ?? throw new ArgumentNullException(nameof(credential));
            _favIconCollector = favIconCollector;

            NameFieldVM = new PassFieldViewModel(credential.NameField);
            OtherFieldVM = new PassFieldViewModel(credential.OtherField);
            AdditionalFields = new ObservableCollection<PassFieldViewModel>(credential.AdditionalFields.Select(f => new PassFieldViewModel(f)));
            Data = CreateData(credential);
            Data.PropertyChanged += Data_PropertyChanged;
            LastModifiedTime = credential.LastModifiedTime;
            CreationTime = credential.CreationTime;
        }

        private static CredentialDataViewModel CreateData(Credential credential) => credential.Type switch
        {
            CredentialType.Login => new LoginViewModel(credential.Login),
            CredentialType.SshKey => new SshKeyViewModel(credential.SshKey),
            _ => throw new NotSupportedException($"Entry type {credential.Type} is not supported.")
        };

        private void Data_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CredentialDataViewModel.Subtitle):
                case nameof(CredentialDataViewModel.SubtitleToolTip):
                case nameof(CredentialDataViewModel.Glyph):
                    OnPropertyChanged(e.PropertyName);
                    break;
                case nameof(LoginViewModel.WebsiteDomain):
                    _favIcon = null;
                    OnPropertyChanged(nameof(FavIcon));
                    break;
            }
        }

        internal CredentialViewModel Clone()
        {
            var cloneModel = Model.Clone();
            var cloneVM = new CredentialViewModel(cloneModel, _favIconCollector)
            {
                PasswordVisible = PasswordVisible,
            };
            return cloneVM;
        }
    }
}
