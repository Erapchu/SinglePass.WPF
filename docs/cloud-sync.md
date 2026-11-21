# Cloud synchronization

How **Sync** keeps the local vault and the cloud copy in step. File format and merge rules are in [vault.md](vault.md).

Code:

- `SinglePass.Core/Services/SyncService.cs` — the sync cycle (`Synchronize`).
- `SinglePass.Core/Services/CredentialsCryptoService.cs` — `MergeFromCloud` (merge + master password choice) and `MergeVaults` (pure merge).
- `SinglePass.Core/Clouds/Services/` — providers: `Download`, `GetRevision`, `Upload`.
- `SinglePass.WPF/ViewModels/CloudSyncViewModel.cs` — the button, password prompt and result dialog.

## In short

Sync = **download → merge → upload if needed**, with a revision check before the upload. There is no separate upload: it was removed because it overwrote the cloud copy blindly.

## Steps

Each attempt:

1. **Download** `singlePass.dat`. Remember its MD5 as the *expected revision*. No file in the cloud → go to step 4.
2. **Decrypt** with the local master password, then with a password entered in a previous attempt, then ask the user until a password works or the user cancels (cancel = sync fails, nothing uploaded).
3. **Merge** into the local vault and write the local file. Deletions (tombstones) are applied here, so deleted credentials do not come back. If the cloud vault used another master password, see [Master password](#master-password).
4. **Decide whether to upload.** Upload is needed when:
   - the cloud has no file;
   - merging the local vault into the downloaded one would change it (the cloud lacks something: new or edited credential, deletion, group change, newer password timestamp);
   - the cloud copy must be re-encrypted with this device's password;
   - the cloud file is in the legacy format.

   Otherwise sync ends: nothing is uploaded.
5. **Check the revision.** Read the local file, ask the provider for the current MD5 of the cloud file. Different from the expected revision (or the file appeared/disappeared) → another device uploaded meanwhile → start the next attempt from step 1.
6. **Upload** the local file as is.

At most **3 attempts**. If every attempt hits a changed revision, sync fails with "The cloud copy kept changing during synchronization". Whatever was merged in earlier attempts stays in the local vault.

```
download ─► decrypt ─► merge (local file written)
                            │
                  cloud up to date? ── yes ─► done
                            │ no
                  revision unchanged? ── no ─► next attempt (max 3)
                            │ yes
                          upload ─► done
```

## Master password

The vault stores `MasterPasswordChangedAt` (set when the password is changed; unknown = oldest). When the cloud vault opens only with a different password, the more recently changed password wins:

| Situation | Result |
|---|---|
| Cloud password changed later | This device re-encrypts its vault with the cloud password and uses it from now on (the user is told). |
| Local password changed later, or neither has a timestamp | The local file keeps the local password; the cloud copy is uploaded re-encrypted with it. |

So a password change on one device reaches the cloud and the other devices instead of being reverted.

## Providers

| | Google Drive | Yandex Disk |
|---|---|---|
| Scope | `drive.file` (only files this app created) | `cloud_api:disk.app_folder` |
| File | Oldest non-trashed file named `singlePass.dat` | `app:/singlePass.dat` |
| Revision | `md5Checksum` | `md5` of the resource |
| Create | One multipart request (metadata + content) | Upload link with `overwrite=true` |

## Result shown to the user

New, changed and deleted credentials; "groups updated"; "master password changed on another device"; "cloud copy updated" or "cloud copy updated and re-encrypted with this device's password"; or "no changes".

## Rules of thumb

- Only one sync per provider at a time; the UI blocks other transfers while one runs.
- Locking the vault cancels the sync.
- Local edits made during a sync are safe: the upload sends the current local file, which includes them.
- The password prompt runs on the UI thread, so `Synchronize` keeps the caller's context (no `ConfigureAwait(false)` there).

## Known limitations

- **Revision check is not atomic.** Neither Google Drive v3 nor Yandex Disk has a conditional write, so steps 5–6 are check-then-write. An upload from another device in the short window between them is overwritten. That device still has the data locally and puts it back on its next sync, so it is lost only if that local vault is lost first.
- **Timestamps come from device clocks.** Credential conflicts, edit-vs-delete and master password changes use them; clock skew can pick the wrong winner.
- **No forced overwrite.** If the cloud vault cannot be decrypted (forgotten password, damaged file), sync cannot proceed; delete the cloud file manually.

## Tests

`SinglePass.Core.Tests`: `VaultMergeTests` (merge, tombstones, format) and `SyncMergeTests` (upload decision, password timestamp, UTC times). Provider calls and `MergeFromCloud` write real files or use the network and are not covered.
