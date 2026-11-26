using System;

namespace Erapkey.Core.Services
{
    /// <summary>
    /// Learns the vault key whenever the user typed the master password: the vault was unlocked with it, the password
    /// was changed, or sync switched to the key of the cloud copy. Windows Hello unlock keeps the key this way.
    /// </summary>
    public interface IVaultKeyObserver
    {
        void OnMasterPasswordEntered(ReadOnlySpan<byte> vaultKey);
    }
}
