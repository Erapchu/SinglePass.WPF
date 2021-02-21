using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class CredentialViewModel : ViewModelBase
    {
        public Credential Model { get; }

        public string Name => Model.Name;

        public CredentialViewModel(Credential credential)
        {
            Model = credential;
        }
    }
}
