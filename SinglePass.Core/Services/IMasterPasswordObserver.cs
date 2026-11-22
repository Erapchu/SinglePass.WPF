namespace SinglePass.Core.Services
{
    /// <summary>
    /// Learns the master password whenever the vault starts using another one: it was changed on this device,
    /// or sync adopted the password of the cloud copy. Windows Hello unlock keeps its protected copy current this way.
    /// </summary>
    public interface IMasterPasswordObserver
    {
        void OnMasterPasswordChanged(string password);
    }
}
