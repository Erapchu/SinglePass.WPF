using Microsoft.Extensions.Logging;
using SinglePass.Core.Helpers;
using System.IO;

namespace SinglePass.Core.Authorization.TokenHolders
{
    public class GoogleDriveTokenHolder(ILogger<GoogleDriveTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "googleDrive.token"), logger,
        Constants.GoogleDriveFilePath);
}
