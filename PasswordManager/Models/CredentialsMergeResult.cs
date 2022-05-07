using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PasswordManager.Models
{
    public class CredentialsMergeResult
    {
        public static CredentialsMergeResult SuccessMergeResult => new CredentialsMergeResult() { Success = true };
        public static CredentialsMergeResult FailureMergeResult => new CredentialsMergeResult() { Success = false };

        public bool Success { get; init; }
        public IList<Credential> NewCredentials { get; } = new List<Credential>();
        public IList<Credential> ChangedCredentials { get; } = new List<Credential>();
        public bool AnyChanges => NewCredentials.Count > 0 || ChangedCredentials.Count > 0;

        public CredentialsMergeResult()
        {

        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var newCreds = false;
            var changedCreds = false;
            if (Success)
            {
                if (NewCredentials.Count > 0)
                {
                    newCreds = true;
                    sb.AppendLine("New credentials added:");
                    sb.Append(string.Join(Environment.NewLine, NewCredentials.Select(c => c.NameField.Value)));
                    sb.AppendLine();
                }
                if (ChangedCredentials.Count > 0)
                {
                    changedCreds = true;
                    sb.AppendLine("Credentials changed:");
                    sb.Append(string.Join(Environment.NewLine, ChangedCredentials.Select(c => c.NameField.Value)));
                    sb.AppendLine();
                }
                if (!newCreds && !changedCreds)
                {
                    sb.AppendLine("No any changes");
                }
            }
            else
            {
                sb.AppendLine("Can't merge credentials");
            }
            
            return sb.ToString();
        }
    }
}
