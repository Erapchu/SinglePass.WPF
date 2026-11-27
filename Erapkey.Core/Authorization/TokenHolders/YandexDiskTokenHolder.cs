using Microsoft.Extensions.Logging;
using Erapkey.Core.Helpers;
using Erapkey.Core.Services;
using System.IO;

namespace Erapkey.Core.Authorization.TokenHolders
{
    public class YandexDiskTokenHolder(IUserDataProtector protector, ILogger<YandexDiskTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "yandexDisk.token"), protector, logger);
}
