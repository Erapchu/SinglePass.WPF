using Microsoft.Extensions.Logging;
using Erapkey.Core.Helpers;
using System.IO;

namespace Erapkey.Core.Authorization.TokenHolders
{
    public class GoogleDriveTokenHolder(ILogger<GoogleDriveTokenHolder> logger) : ProtectedTokenHolder(
        Path.Combine(Constants.LocalAppDataDirectoryPath, "googleDrive.token"), logger,
        Constants.GoogleDriveFilePath);
}
