# Password vault

This document describes how the password vault is stored, changed and synchronized. The code lives in `SinglePass.Core`:

- `Services/CryptoService.cs` — file encryption format.
- `Services/CredentialsCryptoService.cs` — vault in memory, local changes, merge.
- `Services/SyncService.cs` — cloud synchronization, described in [cloud-sync.md](cloud-sync.md).
- `Models/PasswordVault.cs` — vault contents.

Tests for the merge rules and the vault format are in `SinglePass.Core.Tests`.

## File

The vault is a single file, `%LOCALAPPDATA%\SinglePass\singlePass.dat`. The cloud copy has the same name and the same bytes: upload sends the local file as is.

### Keys

The contents are encrypted with the **vault key**: 32 random bytes, created once for a vault (`VaultKey`). The master password does not encrypt the contents; it opens the **password slot**, the vault key encrypted with a key derived from the password. So:

- Unlocking with the password runs Argon2 once: password → slot → vault key → contents. Windows Hello unlock keeps the vault key instead and needs no Argon2 (see README).
- Saving encrypts with the vault key only, without Argon2. The header, including the slot, stays the same.
- While the vault is unlocked, memory holds the vault key, not the password. Locking zeroes it. The one exception is the first sync after the vault was created or converted on this unlock, see [cloud-sync.md](cloud-sync.md#vault-key-and-master-password).
- Changing the master password creates a **new** vault key and slot and re-encrypts the contents. The old password together with an old copy of the file therefore does not open files written after the change.
- All devices that sync a vault end up with the same vault key: it travels inside the cloud file, in the slot, never in the clear (see [cloud-sync.md](cloud-sync.md#vault-key-and-master-password)).

### Encryption format (format version 2)

| Offset | Size | Content |
|---|---|---|
| 0 | 8 | Magic `SPVAULT!` |
| 8 | 1 | Format version, `2` |
| 9 | 4 | Argon2 memory, KiB (little-endian) |
| 13 | 4 | Argon2 iterations (little-endian) |
| 17 | 1 | Argon2 parallelism |
| 18 | 16 | Argon2 salt, random per slot |
| 34 | 16 | Vault key id: the first 16 bytes of HMAC-SHA256(vault key, `"SinglePass vault key id"`) |
| 50 | 12 | Slot: AES-GCM nonce |
| 62 | 32 | Slot: vault key, encrypted with the password key |
| 94 | 16 | Slot: AES-GCM tag |
| 110 | 12 | Contents: AES-GCM nonce, random per write |
| 122 | 16 | Contents: AES-GCM tag |
| 138 | rest | Contents: UTF-8 JSON of `PasswordVault`, encrypted with the vault key |

- Password key: Argon2id v1.3 with the parameters of the header (64 MiB, 3 iterations, parallelism 4 for new slots), 32 bytes, from the UTF-8 master password. When reading, they are checked before Argon2 runs: values below 8 MiB, 1 iteration or parallelism 1 make the file invalid; values above 1 GiB, 32 iterations or parallelism 16 are reported as written by a newer SinglePass. They can be raised later without a new format version, up to these limits; a newer version that goes beyond them makes older versions ask to update.
- Slot: AES-256-GCM with the password key; bytes 0–49 are the associated data.
- Contents: AES-256-GCM with the vault key; the whole header (bytes 0–109, slot included) is the associated data. Any change to the header or the contents fails decryption.
- The key id lets a device see whether a file uses its vault key without the password (sync, backups, Windows Hello). It reveals nothing about the key. After the slot is opened, the key must match its id.
- Key and plaintext buffers are zeroed after use.

The format version byte describes encryption only. The structure of the JSON inside has its own `Version` (see below). A file with a format version above 2 is reported as written by a newer SinglePass (`UnsupportedVaultVersionException`), not as a wrong password.

### Format version 1 (SinglePass 1.x)

| Offset | Size | Content |
|---|---|---|
| 0 | 8 | Magic `SPVAULT!` |
| 8 | 1 | Format version, `1` |
| 9 | 16 | Argon2 salt, random per write |
| 25 | 12 | AES-GCM nonce, random per write |
| 37 | 16 | AES-GCM tag |
| 53 | rest | Ciphertext: UTF-8 JSON of `PasswordVault` |

The key is derived from the password directly (Argon2id, 64 MiB, 3 iterations, parallelism 4) with a new salt on every write. Format 1 files are only read: old backups, cloud history and devices not updated yet keep them. When a local format 1 vault is unlocked, it gets a vault key and is rewritten in format 2; the old file is kept as an `upgrade` backup. If the file cannot be rewritten (read-only, held by another program, disk full), the vault opens anyway and the file stays in format 1 until the next successful write, which converts it; sync writes it before uploading. A format 1 cloud file is converted when the merged local vault is uploaded.

### Legacy format

A file that does not start with the magic is read as the old format: a 16-byte IV followed by AES-CBC ciphertext. The key is a fixed 32-byte template with the UTF-8 password bytes written over its start. Legacy files are only read and are converted like format 1 files.

### Writing

Every write goes to a temporary file `singlePass.dat.<guid>.tmp` and is flushed to disk. The temporary file is then read back: it must start with exactly the header of the vault key in memory (byte for byte, so it holds the same password slot), the key must match the key id, and the contents must decrypt with that key. The password slot itself is checked when the key is made: a new key (new vault, conversion, password change) must open with its password before it is used, which runs Argon2 once more; a key taken from the cloud comes from a slot that just opened with the password. Only then does the temporary file replace `singlePass.dat` with `File.Replace` (or `File.Move` for the first write). A crash or a failed check leaves the old file intact. Backups are described [below](#backups).

## Contents

```jsonc
{
  "Version": 6,
  "Credentials": [ /* Credential */ ],
  "Groups":             { "<group id>":      { "Group": {...} | null, "ModifiedAt": "...", "IsDeleted": false } },
  "Assignments":        { "<credential id>": { "GroupId": "<group id>" | null, "ModifiedAt": "..." } },
  "DeletedCredentials": { "<credential id>": "<deleted at, UTC>" },
  "Trash":              { "<credential id>": { "DeletedAt": "<UTC>" | null, "ModifiedAt": "<UTC>" } },
  "MasterPasswordChangedAt": "<UTC>" | null
}
```

| Version | Change |
|---|---|
| — | A bare JSON array of credentials (before groups). Still readable. |
| 1 | Object with `Credentials`, `Groups`, `Assignments`. |
| 2 | Adds `DeletedCredentials` (credential tombstones) and `MasterPasswordChangedAt`. |
| 3 | Adds entry types: `Credential.Type`, one data block per type (`Login`, `SshKey`). The login fields move from the credential into `Login`. |
| 4 | Adds payment cards: `Credential.Card`. |
| 5 | Adds the trash: `Trash`. |
| 6 | Adds SSH hosts: `Credential.Host`. |

- A vault with a newer `Version` than the application supports is rejected, both locally and on sync. Builds that only know version 1 cannot open a version 2 vault. This is intentional: an old build would drop `DeletedCredentials` on its next write and deleted credentials would return. For the same reason builds that only know version 2 cannot open a version 3 vault: they would not find the login fields, which moved into `Login`, and would drop the SSH key data. Builds that only know version 3 cannot open a version 4 vault: they would drop card data. Builds that only know version 4 cannot open a version 5 vault: they would show entries in the trash as regular ones and drop the trash state. Builds that only know version 5 cannot open a version 6 vault: they would drop host data.
- Such a vault is reported as written by a newer SinglePass (`UnsupportedVaultVersionException`): the unlock window and sync ask to update SinglePass instead of asking for another password. SinglePass 1.x cannot tell the two apart and keeps asking for the password of a newer cloud copy; it does not change the copy.
- Older vaults are upgraded in memory and written as the current version on the next change. For versions before 3, `LoginField`, `PasswordField`, `SiteField`, `Totp` and `PasswordHistory` of every credential are moved into a `Login` object before the JSON is read. This happens in `ReadVault`, so the local vault, backups and the cloud copy are all converted.
- A missing collection is read as empty; an explicit `null` makes the file invalid.

### Credential

A credential holds the fields every entry has, its type and one data block for that type:

```jsonc
{
  "Id": "<guid>",
  "Type": 0,                         // 0 login (also when missing), 1 SSH key, 2 card, 3 host
  "NameField": {...}, "OtherField": {...}, "AdditionalFields": [...],
  "CreationTime": "...", "LastModifiedTime": "...",
  "Login":  { "LoginField": {...}, "PasswordField": {...}, "SiteField": {...}, "Totp": {...} | null, "PasswordHistory": [...] } | null,
  "SshKey": { "PrivateKey": "...", "PublicKey": "...", "Passphrase": "..." } | null,
  "Card":   { "Number": "...", "Holder": "...", "Expiry": "MM/YY", "Cvv": "...", "Pin": "...", "Brand": 2 | null } | null,
  "Host":   { "Hostname": "...", "Port": 2222 | null, "Username": "...", "SshKeyId": "<guid>" | null } | null
}
```

- `Id` — GUID, the identity of the entry. Equality compares `Id` only.
- `Type` — chosen when the entry is created (**New item** menu) and never changes; editing an entry into another type is rejected.
- Only the block of the entry's type is set; the others are null. `Credential.EnsureTypeData` enforces this when a vault is read and when an entry is added or edited, so the rule does not depend on the UI.
- `Login.PasswordHistory` — up to 10 previous passwords, newest first. When a save changes a non-empty password, the old one is added with the current UTC time.
- `SshKey.PrivateKey` is the key file text (OpenSSH or PEM, possibly encrypted with `Passphrase`); `PublicKey` is an `authorized_keys` line.
- `Card.Brand` is the payment network the user chose (`CardBrand`, a number); null means it is detected from the number when shown. The editor accepts only digits in the number, `MM/YY` in the expiry and digits in CVV and PIN (`CardNumbers`, `CardInput`); values saved before these rules stay as they are until edited. A number that fails the Luhn check is still saved, with a warning in the editor.
- `Host.Port` null means 22. `Host.SshKeyId` is the `Id` of an SSH key entry to sign in with. It is a plain reference: deleting or trashing the key does not change the host, which then shows the key as missing. The private key is used through the SSH agent, see [ssh-agent.md](ssh-agent.md).
- `LastModifiedTime`, `CreationTime` — UTC, set by the UI on save. Older vaults stored local time with an offset; the model converts any value to UTC when it is assigned, and the UI shows local time.

Merge, tombstones, groups and backups do not depend on the type: an entry wins or loses as a whole. Password histories are combined only for logins.

#### Adding an entry type

1. A value in `CredentialType` and a data class (for example `CardData`) with a `Clone` method.
2. A property on `Credential`, a line in `EnsureTypeData` and in `Clone`.
3. A new vault `Version`, so that older builds refuse the vault instead of dropping the new block.
4. UI, in `SinglePass.WPF`:
   - a view model in `ViewModels/Entries` derived from `CredentialDataViewModel` (list subtitle, glyph, searchable texts) and a case in `CredentialViewModel.CreateData`;
   - a view and an editor in `Views/Controls/Entries`, registered as `DataTemplate`s for the view model in `CredentialsDetailsControl.xaml`;
   - an item in the **New item** menu (`PasswordsControl.xaml`), a category in the sidebar (`MainWindowViewModel.CategoryNavigation`) and strings.

   The sidebar selects one filter at a time: all entries, a category (entry type) or a group. In a category the add button creates an entry of that type at once; elsewhere it asks for the type.

   The card itself, the list, search, groups and the save flow need no changes: they show the name, notes, additional fields and dates, and take the rest from the view model of the type.

## Local operations

All operations take an in-process lock keyed by the vault path, apply an immutable change to the vault, write the file and only then replace the in-memory vault. If the write fails, memory stays unchanged. A locked vault rejects all changes.

| Operation | Effect |
|---|---|
| Add credential | Appends it, optionally assigns a group. Removes a tombstone with the same `Id`, if any. |
| Edit credential | Replaces it, updates password history, optionally reassigns the group. |
| Move to trash | Sets `Trash[id] = { DeletedAt: UtcNow, ModifiedAt: UtcNow }`. The credential and its group link stay. |
| Restore from trash | Sets `Trash[id] = { DeletedAt: null, ModifiedAt: UtcNow }`. |
| Delete credential | Removes it, its group assignment and trash state, adds tombstone `DeletedCredentials[id] = UtcNow`. Used for deleting from the trash, emptying it and for expired entries. |
| Save group | Stores the group with `ModifiedAt = UtcNow`. Editing a deleted group does nothing. |
| Delete group | Replaces the group with a tombstone (`Group = null`, `IsDeleted = true`). Assignments to it stay but are ignored. |
| Move credentials | Sets the assignment of each existing credential, `ModifiedAt = UtcNow`. |
| Change master password | Checks the current password against the password slot, makes a `password` backup, sets `MasterPasswordChangedAt = UtcNow` and rewrites the vault with a new vault key and a slot for the new password. |
| Lock | Marks the vault locked, clears it and zeroes the vault key in memory, cancels cloud transfers. |

Group assignments that point to a missing or deleted group are treated as "no group".

### Trash

Deleting an entry in the UI moves it to the trash (**Deleted** in the sidebar, `TrashViewModel`). An entry in the trash keeps all its data and its group, but is not shown in the list, the categories, groups, search or the popup: `CredentialsCryptoService.Credentials` leaves it out, `TrashedCredentials` lists it. From the trash an entry can be restored (one or all) or deleted for good (one, or **Empty trash**).

Entries are deleted for good `PasswordVault.TrashRetention` (30 days) after they were moved to the trash. This happens when the vault is unlocked and when the **Deleted** page is opened, as a regular deletion with a tombstone, so it spreads on sync.

Restoring keeps the trash record with `DeletedAt = null` instead of removing it, so on sync the later of moving to the trash and restoring wins.

## Merge

`CredentialsCryptoService.MergeVaults(local, incoming, result)` is a pure function. It returns `local` unchanged when nothing has to be written. Steps, in order:

1. **Tombstones.** Union of both `DeletedCredentials`; for the same id the later time wins.
2. **Credentials.** An unknown `Id` is added. For a known one, the version with the later `LastModifiedTime` wins as a whole (no per-field merge). Password histories of both versions are combined, deduplicated and cut to 10.
3. **Apply tombstones.** A credential with a tombstone is removed, unless its `LastModifiedTime` (converted to UTC) is later than the deletion. In that case the edit wins: the credential stays and the tombstone is dropped.
4. **Groups.** An unknown group is added. A deleted group stays deleted. Otherwise a deletion wins, then the later `ModifiedAt`.
5. **Assignments.** Kept only for credentials that exist after step 3. For the same credential the later `ModifiedAt` wins.
6. **Trash.** Kept only for credentials that exist after step 3. For the same credential the later `ModifiedAt` wins. Entries that end up in the trash are not reported as new or changed.
7. **Master password time.** The later `MasterPasswordChangedAt` is kept.

The result lists new, changed (winning version) and deleted credentials and whether groups changed; the UI shows it after sync. Changes that only touch tombstones or clean up assignments are written but not reported.

Tombstones of credentials and groups are never removed.

## Backups

`Services/VaultBackups.cs` copies the encrypted vault file to `%LOCALAPPDATA%\SinglePass\Backups\singlePass-<UTC yyyyMMdd-HHmmss-fff>-<reason>.dat`. A backup is the file exactly as it was, so it opens with the master password of that moment, and without a password while it uses the current vault key. The newest 20 are kept; older ones are deleted when a new one is made.

| Reason | When |
|---|---|
| `daily` | Before a local change, if the newest backup of any kind is at least a day old. A failure is logged and the change is saved anyway. |
| `sync` | Before a sync writes changes that came from the cloud. |
| `password` | Before the master password is changed, or replaced by the cloud one during sync. |
| `restore` | Before a backup is restored. |
| `upgrade` | Before a vault file of SinglePass 1.x is rewritten in format 2. Once per file: if the newest `upgrade` backup holds the same bytes (an earlier conversion could not write), no new one is made. A failure is logged and the conversion goes on. |

Except for `daily` and `upgrade`, a failed backup cancels the operation.

### Restore

**Settings → Security → Backups** lists the backups. Restoring one (`CredentialsCryptoService.ApplyBackup`) applies it on top of the current vault as a new change and never removes anything:

- credentials missing now are added back; credentials that differ are reverted to the backup version, and the current password goes to the password history;
- credentials created after the backup stay;
- groups and group links of the backup credentials are reverted; a group deleted since the backup comes back under a new id, because a group tombstone always wins on sync;
- entries in the trash now that were not in the trash in the backup are restored from it; entries that were in the trash in the backup are not restored;
- every restored item gets the current time, so the next sync spreads the restore to the cloud copy and other devices instead of undoing it (a restored credential is newer than the tombstone that deleted it).

A backup that uses the current vault key opens without a password; one made before a password change (another key) or by SinglePass 1.x asks for the password of that backup. Restoring a backup by copying the file over `singlePass.dat` also works, but then the next sync merges the newer cloud changes and deletions back in.

## Cloud synchronization

See [cloud-sync.md](cloud-sync.md).

## Known limitations

- **Clock-based conflict resolution.** Credential conflicts, edit-vs-delete and master password changes are decided by timestamps from the device clock. Clock skew picks the wrong winner. Concurrent edits of different fields of one credential are not combined.
- **Backups are local.** They live next to the vault on this device only; losing the device loses them.

Sync-specific limitations are listed in [cloud-sync.md](cloud-sync.md#known-limitations).
