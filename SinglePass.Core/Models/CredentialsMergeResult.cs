using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SinglePass.Core.Models
{
    public class CredentialsMergeResult
    {
        public static CredentialsMergeResult SuccessMergeResult => new CredentialsMergeResult() { Success = true };
        public static CredentialsMergeResult FailureMergeResult => new CredentialsMergeResult() { Success = false };

        public bool Success { get; init; }
        public IList<Credential> NewCredentials { get; } = new List<Credential>();
        public IList<Credential> ChangedCredentials { get; } = new List<Credential>();
        public IList<Credential> DeletedCredentials { get; } = new List<Credential>();
        internal bool GroupChanges { get; set; }

        /// <summary>Entries were moved to the trash or restored from it on another device.</summary>
        internal bool TrashChanges { get; set; }

        /// <summary>This device switched to the master password of the cloud vault.</summary>
        internal bool MasterPasswordAdopted { get; set; }

        /// <summary>The cloud vault used another master password and is re-encrypted with this device's one on upload.</summary>
        internal bool CloudReencrypted { get; set; }

        /// <summary>The cloud copy lacks something the local vault has and has to be uploaded.</summary>
        internal bool CloudNeedsUpdate { get; set; }

        /// <summary>The cloud copy was uploaded during this sync.</summary>
        internal bool CloudUpdated { get; set; }

        /// <summary>Why the sync failed, when it is worth telling the user.</summary>
        internal string FailureReason { get; init; }

        public bool AnyChanges => NewCredentials.Count > 0 || ChangedCredentials.Count > 0 || DeletedCredentials.Count > 0 || GroupChanges || TrashChanges;

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
                    sb.AppendLine(SinglePass.Language.Properties.Resources.NewCredentialsAdded);
                    sb.Append(string.Join(Environment.NewLine, NewCredentials.Select(c => c.NameField.Value)));
                    sb.AppendLine();
                    sb.AppendLine();
                }
                if (ChangedCredentials.Count > 0)
                {
                    changedCreds = true;
                    sb.AppendLine(SinglePass.Language.Properties.Resources.CredentialsChanged);
                    sb.Append(string.Join(Environment.NewLine, ChangedCredentials.Select(c => c.NameField.Value)));
                    sb.AppendLine();
                }
                if (DeletedCredentials.Count > 0)
                {
                    if (newCreds || changedCreds)
                        sb.AppendLine();
                    sb.AppendLine(Localize("CredentialsDeleted"));
                    sb.Append(string.Join(Environment.NewLine, DeletedCredentials.Select(c => c.NameField.Value)));
                    sb.AppendLine();
                }
                if (GroupChanges)
                    sb.AppendLine(Localize("PasswordGroupsSynchronized"));
                if (TrashChanges)
                    sb.AppendLine(Localize("TrashSynchronized"));
                if (MasterPasswordAdopted)
                    sb.AppendLine(Localize("MasterPasswordAdoptedFromCloud"));
                if (CloudUpdated)
                    sb.AppendLine(Localize(CloudReencrypted ? "CloudVaultReencrypted" : "CloudVaultUpdated"));
                if (!AnyChanges && !MasterPasswordAdopted && !CloudUpdated)
                {
                    sb.AppendLine(SinglePass.Language.Properties.Resources.NoAnyChanges);
                }
            }
            else
            {
                sb.AppendLine(FailureReason ?? SinglePass.Language.Properties.Resources.CanTMergeCredentials);
            }

            return sb.ToString();
        }

        private static string Localize(string key) => SinglePass.Core.Localization.LocalizationManager.Instance[key];
    }
}
