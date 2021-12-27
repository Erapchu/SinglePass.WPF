using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PasswordManager.ViewModels
{
    public class CredentialViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<CredentialViewModel> _lazy = new(GetDesignTimeVM);
        public static CredentialViewModel DesignTimeInstance => _lazy.Value;

        private static CredentialViewModel GetDesignTimeVM()
        {
            var fields = Credential.DefaultFields;
            var model = new Credential()
            {
                Fields = fields
            };
            var vm = new CredentialViewModel(model);
            return vm;
        }
        #endregion

        public Credential Model { get; }

        public ObservableCollection<PassFieldViewModel> Fields { get; }

        public string Name => Model.Fields.FirstOrDefault(f => f.Name.Equals("Name")).Value;
        public string Login => Model.Fields.FirstOrDefault(f => f.Name.Equals("Login")).Value;
        public string Password => Model.Fields.FirstOrDefault(f => f.Name.Equals("Password")).Value;
        public string Other => Model.Fields.FirstOrDefault(f => f.Name.Equals("Other")).Value;

        public CredentialViewModel(Credential credential)
        {
            Model = credential ?? throw new ArgumentNullException(nameof(credential));
            Fields = new ObservableCollection<PassFieldViewModel>(credential.Fields.Select(f => new PassFieldViewModel(f)));
        }
    }
}
