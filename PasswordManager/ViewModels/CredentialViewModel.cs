using Microsoft.Toolkit.Mvvm.ComponentModel;
using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class CredentialViewModel : ObservableRecipient
    {
        public Credential Model { get; }

        public string Name => Model.Name;
        public string Login => Model.Login;
        public string Password => Model.Password;
        public string Other => Model.Other;
        public Uri SiteUri => Model.SiteUri;

        public CredentialViewModel(Credential credential)
        {
            Model = credential;
        }
    }
}
