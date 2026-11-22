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
  "Version": 3,
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
| 3 | Adds entry types: `Credential.Type`, one data block per type (`Login`, `SshKey`). The login fields move from the credential into `Login`. |

- A vault with a newer `Version` than the application supports is rejected, both locally and on sync. Builds that only know version 1 cannot open a version 2 vault. This is intentional: an old build would drop `DeletedCredentials` on its next write and deleted credentials would return. For the same reason builds that only know version 2 cannot open a version 3 vault: they would not find the login fields, which moved into `Login`, and would drop the SSH key data.
- Older vaults are upgraded in memory and written as the current version on the next change. For versions before 3, `LoginField`, `PasswordField`, `SiteField`, `Totp` and `PasswordHistory` of every credential are moved into a `Login` object before the JSON is read. This happens in `ReadVault`, so the local vault, backups and the cloud copy are all converted.
- A missing collection is read as empty; an explicit `null` makes the file invalid.

### Credential

A credential holds the fields every entry has, its type and one data block for that type:

```jsonc
{
  "Id": "<guid>",
  "Type": 0,                         // 0 login (also when missing), 1 SSH key
  "NameField": {...}, "OtherField": {...}, "AdditionalFields": [...],
  "CreationTime": "...", "LastModifiedTime": "...",
  "Login":  { "LoginField": {...}, "PasswordField": {...}, "SiteField": {...}, "Totp": {...} | null, "PasswordHistory": [...] } | null,
  "SshKey": { "PrivateKey": "...", "PublicKey": "...", "Passphrase": "..." } | null
}
```

- `Id` — GUID, the identity of the entry. Equality compares `Id` only.
- `Type` — chosen when the entry is created (**New item** menu) and never changes; editing an entry into another type is rejected.
- Only the block of the entry's type is set; the others are null. `Credential.EnsureTypeData` enforces this when a vault is read and when an entry is added or edited, so the rule does not depend on the UI.
- `Login.PasswordHistory` — up to 10 previous passwords, newest first. When a save changes a non-empty password, the old one is added with the current UTC time.
- `SshKey.PrivateKey` is the key file text (OpenSSH or PEM, possibly encrypted with `Passphrase`); `PublicKey` is an `authorized_keys` line.
- `LastModifiedTime`, `CreationTime` — UTC, set by the UI on save. Older vaults stored local time with an offset; the model converts any value to UTC when it is assigned, and the UI shows local time.

Merge, tombstones, groups and backups do not depend on the type: an entry wins or loses as a whole. Password histories are combined only for logins.

#### Adding an entry type

1. A value in `CredentialType` and a data class (for example `CardData`) with a `Clone` method.
2. A property on `Credential`, a line in `EnsureTypeData` and in `Clone`.
3. A new vault `Version`, so that older builds refuse the vault instead of dropping the new block.
4. UI: an item in the **New item** menu, view and edit sections in `CredentialsDetailsControl`, strings.

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
