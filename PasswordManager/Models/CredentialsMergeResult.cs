using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PasswordManager.Models
{
    public class CredentialsMergeResult
    {
        public IList<Credential> NewCredentials { get; } = new List<Credential>();
        public IList<Credential> ChangedCredentials { get; } = new List<Credential>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            var newCreds = false;
            var changedCreds = false;
            if (NewCredentials.Count > 0)
            {
                newCreds = true;
                sb.AppendLine("New credentials added:");
                sb.Append(string.Join(Environment.NewLine, NewCredentials.Select(c => c.NameField.Value)));
            }
            if (ChangedCredentials.Count > 0)
            {
                changedCreds = true;
                sb.AppendLine("Credentials changed:");
                sb.Append(string.Join(Environment.NewLine, ChangedCredentials.Select(c => c.NameField.Value)));
            }
            if (!newCreds && !changedCreds)
            {
                sb.AppendLine("No any changes");
            }
            return sb.ToString();
        }
    }
}
