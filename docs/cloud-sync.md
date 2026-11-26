# Cloud synchronization

How **Sync** keeps the local vault and the cloud copy in step. File format and merge rules are in [vault.md](vault.md).

Code:

- `Erapkey.Core/Services/SyncService.cs` — the sync cycle (`Synchronize`).
- `Erapkey.Core/Services/CredentialsCryptoService.cs` — `MergeFromCloud` (merge + vault key and master password choice) and `MergeVaults` (pure merge).
- `Erapkey.Core/Clouds/Services/` — providers: `Download`, `GetRevision`, `Upload`.
- `Erapkey.Desktop/ViewModels/CloudSyncViewModel.cs` — the button, password prompt and result dialog.

## In short

Sync = **download → merge → upload if needed**, with a revision check before the upload. There is no separate upload: it was removed because it overwrote the cloud copy blindly.

## Steps

Each attempt:

1. **Download** `erapkey.dat`. Remember its MD5 as the *expected revision*. No file in the cloud → go to step 4.
2. **Decrypt.** If the cloud file uses this device's vault key (same key id), it is decrypted with that key: no password, no Argon2. This is the usual case. Otherwise it is tried with a password entered in a previous attempt, then with the password kept for the first sync after the vault was created or converted on this unlock (see below), then the user is asked until a password works or the user cancels (cancel = sync fails, nothing uploaded). A cloud file written by a newer Erapkey fails sync at once with "update Erapkey".
3. **Merge** into the local vault and write the local file. If the merge changes anything, a `sync` backup of the local vault is made first (see [vault.md](vault.md#backups)). Deletions (tombstones) are applied here, so deleted credentials do not come back. If the cloud file uses another vault key, see [Vault key and master password](#vault-key-and-master-password).
4. **Decide whether to upload.** Upload is needed when:
   - the cloud has no file;
   - merging the local vault into the downloaded one would change it (the cloud lacks something: new or edited credential, deletion, group change, newer password timestamp);
   - the cloud file uses another vault key than the local file (it is re-encrypted with this device's key);
   - the cloud file is in format 1 or the legacy format.

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

## Vault key and master password

All devices of a vault should use one vault key (see [vault.md](vault.md#keys)); then sync needs neither the password nor Argon2. The key travels inside the cloud file, in its password slot. A cloud file with another key appears when:

- the master password was changed on another device (a change creates a new key);
- devices converted their Erapkey 1.x files independently, each getting its own random key;
- a new vault was created on this device while the cloud already had one.

The vault stores `MasterPasswordChangedAt` (set when the password is changed; unknown = oldest). Opening such a cloud file needs its password, and then:

| Cloud file | Situation | Result |
|---|---|---|
| Format 2, another key | Cloud password changed later, or at the same time (both unknown included) and the cloud key has the lower key id | This device takes the cloud key and slot and re-encrypts its file with them (a `sync` backup first; a `password` backup when the password differs). If the cloud password differs from the local one, the user is told that the master password changed on another device. |
| Format 2, another key | Local password changed later, or at the same time and the local key has the lower key id | The local file keeps its key; the cloud copy is uploaded with it. |
| Format 1 or legacy | Cloud password changed later and differs | This device switches to the cloud password, with a new key (the user is told). |
| Format 1 or legacy | Otherwise | The local file keeps its key; the cloud copy is uploaded in format 2 with it. |

A tie goes to the lower key id. The rule does not depend on which side is the cloud, so every device and every cloud (Google Drive and Yandex Disk can start with different keys) settles on the same key; letting the cloud win instead would switch a device syncing two clouds back and forth on every sync. For 1.x cloud files a tie keeps the local password, as before; after the upload the cloud file is in format 2 anyway. A password change on one device thus reaches the cloud and the other devices instead of being reverted.

The password is asked only while a cloud uses another key: at most once per device and cloud until the keys agree. To spare the user that question right after typing the password, an unlock that creates the vault or converts a 1.x file keeps the master password in memory until the vault locks (`CredentialsCryptoService.FirstSyncPassword`), and sync tries it. It is forgotten earlier when the password changes, here or by adopting the cloud one. Other unlocks keep only the vault key.

## Providers

| | Google Drive | Yandex Disk |
|---|---|---|
| Scope | `drive.file` (only files this app created) | `cloud_api:disk.app_folder` |
| File | Oldest non-trashed file named `erapkey.dat` | `app:/erapkey.dat` |
| Revision | `md5Checksum` | `md5` of the resource |
| Create | One multipart request (metadata + content) | Upload link with `overwrite=true` |

## Progress shown to the user

While sync runs, the account card shows a progress bar and the current stage from `SyncService.SyncStateChanged(cloudType, text)`: downloading, decrypting, merging, uploading, or "the cloud copy changed, syncing again" when an attempt is repeated. An empty text means sync ended.

After a successful sync the card also shows **Last sync** (local date and time). It is stored per cloud in `commonSettings.json` (`LastSyncTimes`, UTC), so it describes this device only, and it is cleared on sign-out.

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
- **Erapkey 1.x cannot read format 2.** Once one device uploads, devices still on 1.x keep asking for the password of the cloud copy; they do not change it. Update every device.

## Tests

`Erapkey.Core.Tests`: `VaultMergeTests` (merge, tombstones, format), `SyncMergeTests` (upload decision, password timestamp, UTC times) and `VaultKeyServiceTests` (`MergeFromCloud` with the same key, independently converted devices, password changed here or elsewhere, 1.x cloud files; on temporary files). Provider calls and the `SyncService` cycle use the network and are not covered.
