using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.Models
{
    public class Folder
    {
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public PackIconKind Icon { get; set; }
    }
}
