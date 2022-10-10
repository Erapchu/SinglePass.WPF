using CommunityToolkit.Mvvm.ComponentModel;
using SinglePass.WPF.Models;
using SinglePass.WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace SinglePass.WPF.ViewModels
{
    [DebuggerDisplay("{Model}")]
    public class CredentialViewModel : ObservableRecipient
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
        public PassFieldViewModel NameFieldVM { get; }
        public PassFieldViewModel LoginFieldVM { get; }
        public PassFieldViewModel PasswordFieldVM { get; }
        public PassFieldViewModel OtherFieldVM { get; }
        public PassFieldViewModel SiteFieldVM { get; }
        public ObservableCollection<PassFieldViewModel> AdditionalFields { get; }

        public DateTime LastModifiedTime
        {
            get => Model.LastModifiedTime;
            set => Model.LastModifiedTime = value;
        }

        public DateTime CreationTime
        {
            get => Model.CreationTime;
            set => Model.CreationTime = value;
        }

        private bool _passwordVisible;
        public bool PasswordVisible
        {
            get => _passwordVisible;
            set => SetProperty(ref _passwordVisible, value);
        }

        private ImageSource _favIcon;
        public ImageSource FavIcon
        {
            get
            {
                if (_favIcon is null)
                    _favIconCollector.ScheduleGetImage(
                        SiteFieldVM.Value,
                        (image) => SetProperty(ref _favIcon, image, nameof(FavIcon)));

                return _favIcon;
            }
        }

        private ImageSource _favIcon32;
        public ImageSource FavIcon32
        {
            get
            {
                if (_favIcon32 is null)
                    _favIconCollector.ScheduleGetImage(
                        SiteFieldVM.Value,
                        (image) => SetProperty(ref _favIcon32, image, nameof(FavIcon32)),
                        32);

                return _favIcon32;
            }
        }

        public CredentialViewModel(Credential credential, IFavIconCollector favIconCollector)
        {
            Model = credential ?? throw new ArgumentNullException(nameof(credential));
            _favIconCollector = favIconCollector;

            NameFieldVM = new PassFieldViewModel(credential.NameField);
            LoginFieldVM = new PassFieldViewModel(credential.LoginField);
            PasswordFieldVM = new PassFieldViewModel(credential.PasswordField);
            OtherFieldVM = new PassFieldViewModel(credential.OtherField);
            SiteFieldVM = new PassFieldViewModel(credential.SiteField);
            AdditionalFields = new ObservableCollection<PassFieldViewModel>(credential.AdditionalFields.Select(f => new PassFieldViewModel(f)));
            LastModifiedTime = credential.LastModifiedTime;
            CreationTime = credential.CreationTime;
        }

        internal CredentialViewModel Clone()
        {
            var cloneModel = Model.Clone();
            var cloneVM = new CredentialViewModel(cloneModel, _favIconCollector)
            {
                _passwordVisible = _passwordVisible,
            };
            return cloneVM;
        }
    }
}
