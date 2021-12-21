using System;

namespace PasswordManager.Models
{
    public class Credential
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string Other { get; set; }
        public Uri SiteUri { get; set; }
    }
}
