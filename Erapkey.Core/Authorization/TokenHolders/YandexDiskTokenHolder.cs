using Microsoft.Extensions.Logging;
using Erapkey.Core.Helpers;
using System.IO;

namespace Erapkey.Core.Authorization.TokenHolders
{
    public class YandexDiskTokenHolder(ILogger<YandexDiskTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "yandexDisk.token"), logger);
}
