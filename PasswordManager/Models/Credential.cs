using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.Models
{
    public class Credential
    {
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string Other { get; set; }
    }
}
