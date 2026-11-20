using Microsoft.Extensions.Logging;
using SinglePass.Core.Helpers;
using System.IO;

namespace SinglePass.Core.Authorization.TokenHolders
{
    public class YandexDiskTokenHolder(ILogger<YandexDiskTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "yandexDisk.token"), logger);
}
