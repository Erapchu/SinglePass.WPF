using Microsoft.Extensions.Logging;
using SinglePass.WPF.Helpers;
using System.IO;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public class YandexDiskTokenHolder(ILogger<YandexDiskTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "yandexDisk.token"), logger);
}
