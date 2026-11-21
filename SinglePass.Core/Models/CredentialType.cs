namespace SinglePass.Core.Models
{
    /// <summary>Kind of vault entry. Stored as a number; a missing value means <see cref="Login"/>.</summary>
    public enum CredentialType
    {
        Login = 0,
        SshKey = 1,
    }
}
