using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace PasswordManager.ViewModels
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

        private readonly FavIconService _favIconService;

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
        public ImageSource FavIcon => _favIcon ??= _favIconService.GetImage(SiteFieldVM.Value);

        public CredentialViewModel(Credential credential, FavIconService favIconService)
        {
            Model = credential ?? throw new ArgumentNullException(nameof(credential));
            _favIconService = favIconService;

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
            var cloneVM = new CredentialViewModel(cloneModel, _favIconService)
            {
                _passwordVisible = _passwordVisible,
            };
            return cloneVM;
        }
    }
}
