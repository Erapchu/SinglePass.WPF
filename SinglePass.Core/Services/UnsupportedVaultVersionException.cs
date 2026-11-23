using System;

namespace SinglePass.Core.Services
{
    /// <summary>
    /// The vault was written by a newer SinglePass: its encryption format or its contents version is unknown here.
    /// Unlike a wrong password, asking for another password cannot help; the application has to be updated.
    /// </summary>
    public sealed class UnsupportedVaultVersionException : Exception
    {
        public UnsupportedVaultVersionException(string message) : base(message)
        {
        }
    }
}
