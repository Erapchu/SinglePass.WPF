using Erapkey.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Erapkey.Core.Services.Browser
{
    /// <summary>A browser asking to pair, for the confirmation window.</summary>
    /// <param name="Name">The name the extension gave, such as "Chrome".</param>
    /// <param name="Browser">The browser process, such as "chrome.exe", or "?" when unknown.</param>
    /// <param name="Code">The six digits the extension shows too.</param>
    public sealed record BrowserPairingRequest(string Name, string Browser, string Code);

    /// <summary>What the browser server needs from the application. Implemented by Erapkey.</summary>
    public interface IBrowserBackend
    {
        /// <summary>Entries of the unlocked vault (not in the trash), or null while it is locked.</summary>
        Task<IReadOnlyList<Credential>> GetCredentialsAsync(CancellationToken cancellationToken);

        /// <summary>Shows the unlock window and waits up to a minute; true when the vault is unlocked.</summary>
        Task<bool> UnlockAsync(CancellationToken cancellationToken);

        /// <summary>Asks the user to allow a browser; false when denied or not answered in time.</summary>
        Task<bool> ConfirmPairingAsync(BrowserPairingRequest request, CancellationToken cancellationToken);

        /// <summary>Brings Erapkey to the front with the entry selected.</summary>
        Task OpenEntryAsync(Guid entryId, CancellationToken cancellationToken);
    }
}
