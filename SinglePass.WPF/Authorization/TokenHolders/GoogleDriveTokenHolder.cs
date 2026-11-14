using Microsoft.Extensions.Logging;
using SinglePass.WPF.Helpers;
using System.IO;

namespace SinglePass.WPF.Authorization.TokenHolders
{
    public class GoogleDriveTokenHolder(ILogger<GoogleDriveTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "googleDrive.token"), logger,
        Constants.GoogleDriveFilePath);
}
