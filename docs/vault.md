# Password vault

This document describes how the password vault is stored, changed and synchronized. The code lives in `SinglePass.Core`:

- `Services/CryptoService.cs` — file encryption format.
- `Services/CredentialsCryptoService.cs` — vault in memory, local changes, merge.
- `Services/SyncService.cs` — cloud download/merge and upload.
- `Models/PasswordVault.cs` — vault contents.

Tests for the merge rules and the vault format are in `SinglePass.Core.Tests`.

## File

The vault is a single file, `%LOCALAPPDATA%\SinglePass\singlePass.dat`. The cloud copy has the same name and the same bytes: upload sends the local file as is.

### Encryption format (format version 1)

| Offset | Size | Content |
|---|---|---|
| 0 | 8 | Magic `SPVAULT!` |
| 8 | 1 | Format version, `1` |
| 9 | 16 | Argon2 salt, random per write |
| 25 | 12 | AES-GCM nonce, random per write |
| 37 | 16 | AES-GCM tag |
| 53 | rest | Ciphertext: UTF-8 JSON of `PasswordVault` |

- Key: Argon2id v1.3, 64 MiB memory, 3 iterations, parallelism 4, 32-byte output, from the UTF-8 master password.
- Cipher: AES-256-GCM. The 37-byte header is the associated data, so any change to it fails decryption.
- KDF parameters are fixed by the format version byte. Changing them requires a new format version.
- Key and plaintext buffers are zeroed after use.

The format version byte describes encryption only. The structure of the JSON inside has its own `Version` (see below).

### Legacy format

A file that does not start with the magic is read as the old format: a 16-byte IV followed by AES-CBC ciphertext. The key is a fixed 32-byte template with the UTF-8 password bytes written over its start. Legacy files are only read. After a local legacy vault is unlocked, it is immediately rewritten in the current format. A legacy cloud file is converted only when the merged local vault is uploaded.

### Writing

Every write goes to a temporary file `singlePass.dat.<guid>.tmp`, is flushed to disk and then replaces `singlePass.dat` with `File.Replace` (or `File.Move` for the first write). A crash during a write leaves the old file intact. No backup copy is kept.

## Contents

```jsonc
{
  "Version": 2,
  "Credentials": [ /* Credential */ ],
  "Groups":             { "<group id>":      { "Group": {...} | null, "ModifiedAt": "...", "IsDeleted": false } },
  "Assignments":        { "<credential id>": { "GroupId": "<group id>" | null, "ModifiedAt": "..." } },
  "DeletedCredentials": { "<credential id>": "<deleted at, UTC>" }
}
```

| Version | Change |
|---|---|
| — | A bare JSON array of credentials (before groups). Still readable. |
| 1 | Object with `Credentials`, `Groups`, `Assignments`. |
| 2 | Adds `DeletedCredentials` (credential tombstones). |

- A vault with a newer `Version` than the application supports is rejected, both locally and on sync. Builds that only know version 1 cannot open a version 2 vault. This is intentional: an old build would drop `DeletedCredentials` on its next write and deleted credentials would return.
- Older vaults are upgraded in memory and written as the current version on the next change.
- A missing collection is read as empty; an explicit `null` makes the file invalid.

### Credential

`Credential` holds the fixed fields (name, login, password, notes, site), additional fields, TOTP setup and:

- `Id` — GUID, the identity of the entry. Equality compares `Id` only.
- `LastModifiedTime`, `CreationTime` — set by the UI from the local clock (`DateTime.Now`) on save.
- `PasswordHistory` — up to 10 previous passwords, newest first. When a save changes a non-empty password, the old one is added with the current UTC time.

## Local operations

All operations take an in-process lock keyed by the vault path, apply an immutable change to the vault, write the file and only then replace the in-memory vault. If the write fails, memory stays unchanged. A locked vault rejects all changes.

| Operation | Effect |
|---|---|
| Add credential | Appends it, optionally assigns a group. Removes a tombstone with the same `Id`, if any. |
| Edit credential | Replaces it, updates password history, optionally reassigns the group. |
| Delete credential | Removes it and its group assignment, adds tombstone `DeletedCredentials[id] = UtcNow`. |
| Save group | Stores the group with `ModifiedAt = UtcNow`. Editing a deleted group does nothing. |
| Delete group | Replaces the group with a tombstone (`Group = null`, `IsDeleted = true`). Assignments to it stay but are ignored. |
| Move credentials | Sets the assignment of each existing credential, `ModifiedAt = UtcNow`. |
| Change master password | Checks the current password and rewrites the same vault with the new one. |
| Lock | Marks the vault locked, clears it and the password from memory, cancels cloud transfers. |

Group assignments that point to a missing or deleted group are treated as "no group".

## Merge

`CredentialsCryptoService.MergeVaults(local, incoming, result)` is a pure function. It returns `local` unchanged when nothing has to be written. Steps, in order:

1. **Tombstones.** Union of both `DeletedCredentials`; for the same id the later time wins.
2. **Credentials.** An unknown `Id` is added. For a known one, the version with the later `LastModifiedTime` wins as a whole (no per-field merge). Password histories of both versions are combined, deduplicated and cut to 10.
3. **Apply tombstones.** A credential with a tombstone is removed, unless its `LastModifiedTime` (converted to UTC) is later than the deletion. In that case the edit wins: the credential stays and the tombstone is dropped.
4. **Groups.** An unknown group is added. A deleted group stays deleted. Otherwise a deletion wins, then the later `ModifiedAt`.
5. **Assignments.** Kept only for credentials that exist after step 3. For the same credential the later `ModifiedAt` wins.

The result lists new, changed (winning version) and deleted credentials and whether groups changed; the UI shows it after sync. Changes that only touch tombstones or clean up assignments are written but not reported.

Tombstones of credentials and groups are never removed.

## Cloud synchronization

Providers: Google Drive (scope `drive.file`, file found by name among non-trashed files the app can see) and Yandex Disk (`app:/singlePass.dat` in the application folder).

- **Sync** downloads the cloud file, decrypts it with the current master password (asking for another password while decryption fails), merges it into the local vault and writes the local file with the **local** password. It does not upload.
- **Upload** sends the local file and overwrites the cloud copy unconditionally.

Only one transfer per provider runs at a time. Locking the vault cancels transfers.

## Known limitations

- **Lost updates.** Upload has no precondition (no ETag/revision check), and Sync and Upload are separate steps. Uploading without syncing first, or another device uploading between them, overwrites changes in the cloud copy.
- **Clock-based conflict resolution.** Credential conflicts and edit-vs-delete are decided by `LastModifiedTime` from the device clock. Clock skew picks the wrong winner. Concurrent edits of different fields of one credential are not combined.
- **Master password follows the uploading device.** After Sync with a different cloud password, the local file keeps the local password, so the next Upload re-encrypts the cloud copy with it.
- **Google Drive upload** creates a file and renames it in a second request. If the rename fails, an unnamed file is left behind and the next upload creates another one. With several files of the same name, an arbitrary one is used.
- **No backups.** A wrong merge or deletion cannot be undone from local data.
