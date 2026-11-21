# Password vault

This document describes how the password vault is stored, changed and synchronized. The code lives in `SinglePass.Core`:

- `Services/CryptoService.cs` — file encryption format.
- `Services/CredentialsCryptoService.cs` — vault in memory, local changes, merge.
- `Services/SyncService.cs` — cloud synchronization, described in [cloud-sync.md](cloud-sync.md).
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

Every write goes to a temporary file `singlePass.dat.<guid>.tmp`, is flushed to disk and then replaces `singlePass.dat` with `File.Replace` (or `File.Move` for the first write). A crash during a write leaves the old file intact. Backups are described [below](#backups).

## Contents

```jsonc
{
  "Version": 2,
  "Credentials": [ /* Credential */ ],
  "Groups":             { "<group id>":      { "Group": {...} | null, "ModifiedAt": "...", "IsDeleted": false } },
  "Assignments":        { "<credential id>": { "GroupId": "<group id>" | null, "ModifiedAt": "..." } },
  "DeletedCredentials": { "<credential id>": "<deleted at, UTC>" },
  "MasterPasswordChangedAt": "<UTC>" | null
}
```

| Version | Change |
|---|---|
| — | A bare JSON array of credentials (before groups). Still readable. |
| 1 | Object with `Credentials`, `Groups`, `Assignments`. |
| 2 | Adds `DeletedCredentials` (credential tombstones) and `MasterPasswordChangedAt`. |

- A vault with a newer `Version` than the application supports is rejected, both locally and on sync. Builds that only know version 1 cannot open a version 2 vault. This is intentional: an old build would drop `DeletedCredentials` on its next write and deleted credentials would return.
- Older vaults are upgraded in memory and written as the current version on the next change.
- A missing collection is read as empty; an explicit `null` makes the file invalid.

### Credential

`Credential` holds the fixed fields (name, login, password, notes, site), additional fields, TOTP setup and:

- `Id` — GUID, the identity of the entry. Equality compares `Id` only.
- `LastModifiedTime`, `CreationTime` — UTC, set by the UI on save. Older vaults stored local time with an offset; the model converts any value to UTC when it is assigned, and the UI shows local time.
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
| Change master password | Checks the current password, makes a `password` backup, sets `MasterPasswordChangedAt = UtcNow` and rewrites the vault with the new password. |
| Lock | Marks the vault locked, clears it and the password from memory, cancels cloud transfers. |

Group assignments that point to a missing or deleted group are treated as "no group".

## Merge

`CredentialsCryptoService.MergeVaults(local, incoming, result)` is a pure function. It returns `local` unchanged when nothing has to be written. Steps, in order:

1. **Tombstones.** Union of both `DeletedCredentials`; for the same id the later time wins.
2. **Credentials.** An unknown `Id` is added. For a known one, the version with the later `LastModifiedTime` wins as a whole (no per-field merge). Password histories of both versions are combined, deduplicated and cut to 10.
3. **Apply tombstones.** A credential with a tombstone is removed, unless its `LastModifiedTime` (converted to UTC) is later than the deletion. In that case the edit wins: the credential stays and the tombstone is dropped.
4. **Groups.** An unknown group is added. A deleted group stays deleted. Otherwise a deletion wins, then the later `ModifiedAt`.
5. **Assignments.** Kept only for credentials that exist after step 3. For the same credential the later `ModifiedAt` wins.
6. **Master password time.** The later `MasterPasswordChangedAt` is kept.

The result lists new, changed (winning version) and deleted credentials and whether groups changed; the UI shows it after sync. Changes that only touch tombstones or clean up assignments are written but not reported.

Tombstones of credentials and groups are never removed.

## Backups

`Services/VaultBackups.cs` copies the encrypted vault file to `%LOCALAPPDATA%\SinglePass\Backups\singlePass-<UTC yyyyMMdd-HHmmss-fff>-<reason>.dat`. A backup is the file exactly as it was, so it opens with the master password of that moment. The newest 20 are kept; older ones are deleted when a new one is made.

| Reason | When |
|---|---|
| `daily` | Before a local change, if the newest backup of any kind is at least a day old. A failure is logged and the change is saved anyway. |
| `sync` | Before a sync writes changes that came from the cloud. |
| `password` | Before the master password is changed, or replaced by the cloud one during sync. |
| `restore` | Before a backup is restored. |

Except for `daily`, a failed backup cancels the operation.

### Restore

**Settings → Security → Backups** lists the backups. Restoring one (`CredentialsCryptoService.ApplyBackup`) applies it on top of the current vault as a new change and never removes anything:

- credentials missing now are added back; credentials that differ are reverted to the backup version, and the current password goes to the password history;
- credentials created after the backup stay;
- groups and group links of the backup credentials are reverted; a group deleted since the backup comes back under a new id, because a group tombstone always wins on sync;
- every restored item gets the current time, so the next sync spreads the restore to the cloud copy and other devices instead of undoing it (a restored credential is newer than the tombstone that deleted it).

The backup is decrypted with the current master password first; if that fails, the user is asked for the password of that backup. Restoring a backup by copying the file over `singlePass.dat` also works, but then the next sync merges the newer cloud changes and deletions back in.

## Cloud synchronization

See [cloud-sync.md](cloud-sync.md).

## Known limitations

- **Clock-based conflict resolution.** Credential conflicts, edit-vs-delete and master password changes are decided by timestamps from the device clock. Clock skew picks the wrong winner. Concurrent edits of different fields of one credential are not combined.
- **Backups are local.** They live next to the vault on this device only; losing the device loses them.

Sync-specific limitations are listed in [cloud-sync.md](cloud-sync.md#known-limitations).
