using Microsoft.Extensions.Logging;
using Erapkey.Core.Helpers;
using Erapkey.Core.Services;
using System.IO;

namespace Erapkey.Core.Authorization.TokenHolders
{
    public class GoogleDriveTokenHolder(IUserDataProtector protector, ILogger<GoogleDriveTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "googleDrive.token"), protector, logger,
        Constants.GoogleDriveFilePath);
}
