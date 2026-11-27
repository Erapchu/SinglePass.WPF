# Passkeys (specification)

Status: **not started**. This document describes how Erapkey stores passkeys (WebAuthn discoverable credentials) and how sites reach them: through the Erapkey browser extension and, on Windows 11, as a passkey provider of the operating system.

Planned code: `Erapkey.Core/Models/PasskeyData.cs`, `Erapkey.Core/Services/Passkeys/` (authenticator, rpId rules, CBOR and COSE), `Erapkey.Core/Services/Browser/` (new requests), `Erapkey.Desktop/Services/Passkeys/` (confirmation, Windows provider), `extension/src/webauthn/`. Tests: `Erapkey.Core.Tests/PasskeyAuthenticatorTests.cs`, `PasskeyMergeTests.cs`, `RpIdTests.cs`, and the interop tests of the extension.

## Goals

- Create and use passkeys on websites in Chrome and Edge, with the private keys kept in the vault and synced like the rest of it.
- Keep a passkey together with the password and TOTP of the same account, in one login entry.
- Work on Windows 10 (browser extension) and offer Erapkey in the Windows passkey dialog on Windows 11 (plugin provider), with one implementation of the authenticator for both.
- Never hand out a private key: sites and the extension receive signatures only.

Not in the first version: passkeys for native applications other than browsers on Windows 10, hybrid sign-in (QR code, phone as authenticator), the extensions `prf`/`hmac-secret`, `largeBlob` and `credBlob`, attestation other than `none`, import and export (see [Later](#later)).

## Terms

| Term | Meaning |
|---|---|
| RP, rpId | Relying party: the site. Its ID is a domain, such as `github.com`. |
| Credential ID | Random bytes that name one passkey to the site. |
| User handle | Opaque ID of the account at the site (up to 64 bytes), returned on sign-in. |
| UP, UV | User presence (the user did something) and user verification (PIN, biometrics or password). |
| Discoverable | A passkey the authenticator can list for an rpId without the site naming it. All Erapkey passkeys are discoverable. |
| Provider | The program that keeps the passkey: Erapkey, Windows Hello, a phone, a security key. |

## Data model

A passkey belongs to a **login entry**, not to a new entry type. A password, a TOTP and a passkey of one account are one entry, as in other password managers; merge, trash, groups and backups work without changes. A passkey with no matching login creates a new login without a password.

`LoginData` gets a list:

```jsonc
"Login": {
  "LoginField": {...}, "PasswordField": {...}, "SiteField": {...}, "Totp": {...} | null, "PasswordHistory": [...],
  "Passkeys": [
    {
      "CredentialId": "<base64url>",        // 16 random bytes
      "RpId": "github.com",
      "RpName": "GitHub",
      "UserHandle": "<base64url>",
      "UserName": "alice",
      "UserDisplayName": "Alice",
      "Algorithm": -7,                       // COSE: -7 ES256; -8 EdDSA and -257 RS256 later
      "PrivateKey": "<base64 PKCS#8>",       // null when Deleted
      "CreationTime": "<UTC>",
      "Deleted": false,
      "ModifiedAt": "<UTC>"
    }
  ]
}
```

- `Passkeys` is missing or empty for most logins. More than one item is allowed (an account with passkeys for two rpIds, or a passkey recreated by the site), though the UI shows one per account in most cases.
- `PrivateKey` is the only secret. It never leaves `Erapkey.exe`: not to the extension, not to Windows, not to the clipboard.
- The signature counter is always **0** and is not stored. Synced passkeys cannot keep a counter consistent across devices, the WebAuthn spec allows 0, and a counter that changes on every sign-in would make every use a vault change to sync.
- A removed passkey stays as a tombstone: `Deleted: true`, `PrivateKey: null`, the other fields kept so the UI can say what was removed. See [Merge](#merge).
- `CredentialId`, `UserHandle` and `RpId` are read-only in the editor. The user may change `UserName` and `UserDisplayName` (the site does not see these) and remove the passkey.

### Vault version

`PasswordVault.CurrentVersion` becomes **7**: an older build would drop `Passkeys` on its next write. The table in [vault.md](vault.md#contents) gets a line "7 — adds passkeys: `Login.Passkeys`", and `LoginData.Clone` copies the list.

### Merge

Today a credential wins or loses as a whole by `LastModifiedTime` ([vault.md](vault.md#merge)). For passkeys this is not enough: a passkey created on one device and a password changed on another before sync would lose the passkey, and the user may be unable to sign in again. So, like the password history, passkeys of both versions are combined:

1. The winning credential is chosen as now.
2. Its `Passkeys` become the union of both versions by `CredentialId`.
3. For an ID present in both, the item with the later `ModifiedAt` wins; a tombstone wins over a live item with the same `ModifiedAt`.
4. Tombstones older than `PasswordVault.TrashRetention` are dropped on write.

A passkey moved to another entry (see [UI](#ui-in-erapkey)) is a tombstone in the old entry and a new item with the same `CredentialId` in the new one; the duplicate check below keeps only the live item when looking up a credential ID.

## Authenticator

`Erapkey.Core/Services/Passkeys/PasskeyAuthenticator.cs` implements the two operations of a CTAP2 authenticator, independent of the channel. It is pure .NET (no Windows APIs), so it can serve macOS later.

### Create (`authenticatorMakeCredential`)

Input: `rpId`, `rpName`, user (handle, name, display name), `clientDataHash`, the list of accepted algorithms, the exclude list, whether UV was done.

1. If the vault has a live passkey for this `rpId` whose `CredentialId` is in the exclude list: after the user confirms, fail with `exists` (the site maps it to `InvalidStateError`).
2. If none of the accepted algorithms is supported (ES256 in the first version): `not_supported`.
3. Generate a P-256 key (`ECDsa.Create(ECCurve.NamedCurves.nistP256)`) and 16 random bytes for the credential ID.
4. If a live passkey with the same `rpId` and `UserHandle` exists, it is replaced (the site recreated it); otherwise the passkey goes into the login chosen in the confirmation window, or into a new login (`NameField` = `rpName`, `SiteField` = `https://<rpId>`, `LoginField` = user name).
5. Return the attestation object with format `none`:

```
attestationObject = CBOR { "fmt": "none", "attStmt": {}, "authData": authenticatorData }

authenticatorData =
  SHA-256(rpId)                  32
  flags                           1   UP 0x01 | UV 0x04 (only when done) | BE 0x08 | BS 0x10 | AT 0x40
  signCount = 0                   4   big-endian
  AAGUID                         16   Erapkey's, see below
  credentialId length             2   big-endian
  credentialId                   16
  credentialPublicKey             COSE_Key, CTAP2 canonical CBOR:
                                      { 1: 2, 3: -7, -1: 1, -2: x (32 bytes), -3: y (32 bytes) }
```

BE and BS (backup eligible, backed up) are always set: the passkey is synced with the vault.

### Get (`authenticatorGetAssertion`)

Input: `rpId`, `clientDataHash`, the allow list (may be empty), the chosen passkey, whether UV was done.

1. Candidates: live passkeys whose `RpId` equals the requested `rpId` exactly (the site asks for its rpId; the origin check is done before, see [rpId rules](#rpid-rules)). With a non-empty allow list, only those IDs.
2. The user picks one in the confirmation window (or it was picked in the inline list); no candidates: `not_found`.
3. `authenticatorData` = `SHA-256(rpId)`, flags `UP | UV? | BE | BS`, counter 0.
4. `signature` = ECDSA P-256 SHA-256 over `authenticatorData || clientDataHash`, DER-encoded (`DSASignatureFormat.Rfc3279DerSequence`).
5. Return the credential ID, `authenticatorData`, `signature` and the user handle.

### AAGUID

Erapkey has one fixed random AAGUID, a constant in `PasskeyAuthenticator`. Sites such as Google and Microsoft accounts show the provider name and icon by AAGUID from the community list [passkey-authenticator-aaguids](https://github.com/passkeydeveloper/passkey-authenticator-aaguids); Erapkey is added there with its icon once released. The Windows provider registers the same AAGUID in its `authenticatorGetInfo`.

### CBOR

`System.Formats.Cbor` in canonical CTAP2 mode (`CborConformanceMode.Ctap2Canonical`). The browser path needs only encoding (COSE key, attestation object); the Windows path also gets decoding helpers from Windows (see below).

### rpId rules

`Erapkey.Core/Services/Passkeys/RpId.cs`, for the browser path (Windows checks the origin itself):

1. The origin must be `https`, or `http://localhost` (also `*.localhost`) for development.
2. The rpId defaults to the origin's host. A given rpId must equal the host or be a parent domain of it (`login.example.com` may use `example.com`), compared label by label after IDN-to-punycode and lowercase, as `WebsiteMatcher` does.
3. The rpId must not be a public suffix (`co.uk`, `github.io`): `PublicSuffixList` decides.
4. IP addresses are accepted only as the exact host.

These rules match the WebAuthn "registrable domain suffix" check that browsers do.

### Tests

The authenticator output is checked by a real WebAuthn relying party library ([Fido2NetLib](https://github.com/passwordless-lib/fido2-net-lib)), referenced by `Erapkey.Core.Tests` only: register with `MakeCredential`, then verify a `GetAssertion` for the same credential. Plus fixed vectors for the COSE key and `authenticatorData`, the rpId rules and the merge rules.

## User consent and verification

Every create and every get needs the user's action in Erapkey or the inline list; there is no silent signing. The UV flag is set only when the user was actually verified:

| Channel | UV |
|---|---|
| Browser extension | Windows Hello gesture through the existing `NgcKey` (sign a random challenge with the unlock key), when Windows Hello unlock is set up. Otherwise the master password in the confirmation window, when the site requires UV (`userVerification: "required"`). |
| Windows provider | `WebAuthNPluginPerformUserVerification`: Windows shows its own Windows Hello prompt with the name of Erapkey. |

A locked vault first shows the unlock window, as an SSH agent request does (`SshAgentKeyStore`), and waits up to a minute. Unlocking counts as UV for that request.

## Channel 1: browser extension

Works on Windows 10 and 11, in every Chromium-based browser with the extension, later in Firefox. Builds on the existing pairing, session and pipe ([browser-extension.md](browser-extension.md)).

### Interception

```
page ── navigator.credentials.create/get
  │  webauthn-main.ts   MAIN world, document_start, all frames: replaces the two methods
  │  window.postMessage
  ▼
webauthn-relay.ts       isolated world content script
  │  chrome.runtime.sendMessage
  ▼
background              takes the origin from sender.origin, never from the page
  │  session (existing), requests below
  ▼
Erapkey.exe             rpId check, confirmation window, PasskeyAuthenticator
```

- The MAIN world script replaces `navigator.credentials.create` and `get` for `publicKey` requests only, and `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable`, `isConditionalMediationAvailable` and `getClientCapabilities` (all return true while Erapkey is reachable).
- The origin comes from `sender.origin` of the relay message. A page that posts fake messages to the relay can only ask for its own origin.
- The background builds nothing secret: Erapkey builds `clientDataJSON` from the origin and the challenge, hashes it and signs, and returns the JSON so the page gets exactly the bytes that were signed. `clientDataJSON` = `{"type":"webauthn.create"|"webauthn.get","challenge":"<base64url>","origin":"<origin>","crossOrigin":false}`, in this order.
- The answer is turned into an object with the prototype of `PublicKeyCredential` and of `AuthenticatorAttestationResponse` / `AuthenticatorAssertionResponse`, with `getTransports()` (`["internal"]`), `getAuthenticatorData()`, `getPublicKey()` (SPKI), `getPublicKeyAlgorithm()`, `getClientExtensionResults()` (`credProps: { rk: true }` when asked) and `toJSON()`, so that site libraries that check types or call these work.
- Errors map to `DOMException`: `cancelled` → `NotAllowedError`, `exists` → `InvalidStateError`, `not_supported` → `NotSupportedError`.
- **Fallback to the browser**: the original methods are kept. They are called when Erapkey is not running, the browser is not paired, the page is a cross-origin frame (first version), the request has `mediation: "conditional"` and the user picks nothing, or the user presses *Use another device or security key* in the confirmation window. The page then sees the browser's own dialog, as without the extension.
- The `signal` (`AbortSignal`) of the request cancels it: the background sends `cancel { requestId }` and Erapkey closes its window.

### Conditional mediation (autofill)

When the page calls `get` with `mediation: "conditional"`, the extension asks `getPasskeys` for the origin and adds the passkeys to the inline menu of the login field (the content script of [browser-extension.md](browser-extension.md#extension), not built yet). Choosing one sends `passkeyGet` with that credential ID; the click is the consent, as for a login fill. Until the inline menu exists, conditional requests go to the browser.

### Requests

Added to the existing session. The protocol version stays `1`; `status` and `hello` get `features: ["passkeys"]`, so a newer extension can tell whether the running Erapkey supports passkeys and falls back to the browser when not. The version check is an equality, so raising it would break every older extension.

| Request | Answer | Notes |
|---|---|---|
| `passkeyCreate { requestId, origin, rp, user, challenge, pubKeyCredParams, excludeCredentials, authenticatorSelection, extensions }` | `{ credentialId, clientDataJSON, attestationObject, authenticatorData, publicKey, publicKeyAlgorithm }` | All binary values base64url. Shows the confirmation window. |
| `passkeyGet { requestId, origin, rpId, challenge, allowCredentials, userVerification, credentialId? }` | `{ credentialId, clientDataJSON, authenticatorData, signature, userHandle }` | `credentialId` is set when the user already chose in the inline list. |
| `getPasskeys { origin, rpId }` | `passkeys: [{ credentialId, entryId, userName, userDisplayName }]` | For the inline list. No keys. Only after the rpId check. |
| `cancel { requestId }` | `{}` | Closes the confirmation window of that request. |

New errors: `cancelled`, `exists`, `not_supported`, `fallback` (the user chose another device: the extension calls the browser).

`IBrowserBackend` gets `ConfirmPasskeyCreateAsync` and `ConfirmPasskeyGetAsync` (the window), and a way to save the new passkey into the vault.

### Manifest

Interception needs content scripts on every page:

```json
"content_scripts": [
  { "matches": ["https://*/*", "http://localhost/*"], "js": ["webauthn-main.js"], "run_at": "document_start", "all_frames": true, "world": "MAIN" },
  { "matches": ["https://*/*", "http://localhost/*"], "js": ["webauthn-relay.js"], "run_at": "document_start", "all_frames": true }
]
```

This is a much wider permission than today's `activeTab` and is shown to the user on install and update; the store review looks at it too. The scripts contain no vault data and only forward WebAuthn calls.

### Confirmation window

`PasskeyConfirmWindow` in `Erapkey.Desktop`, shown over other windows like `SshAgentConfirmWindow`:

- **Create**: *Save a passkey for github.com?* The user name from the site, the browser, and where to save it: the logins whose site matches the rpId (best first) or *New login*. Buttons: **Save**, **Use another device**, **Cancel**.
- **Get**: *Sign in to github.com* with the list of matching passkeys (user name, entry name). Buttons: **Sign in**, **Use another device**, **Cancel**.
- When UV is needed, **Save** / **Sign in** asks for Windows Hello or the master password first.

## Channel 2: Windows passkey provider

Windows 11 lets a third-party program act as a passkey provider through the plugin authenticator API (`webauthnplugin.h`, `pluginauthenticator.idl`). Chrome and Edge on Windows call the Windows WebAuthn API, so with Erapkey enabled they show it in the system passkey dialog and in autofill without the extension.

### Requirements

- Windows 11 24H2 (build 26100) or 25H2 (26200), revision 6725 or later (the November 2025 update). Not available on Windows 10.
- Windows SDK 10.0.26100.7175 or later, for the headers that are ported to C#.
- **Package identity.** The API is meant for packaged apps: the COM server is declared in the package manifest, and a call from an unpackaged process was reported to fail with `0x80073D54` (`APPMODEL_ERROR_NO_PACKAGE`). Erapkey is installed by Velopack without a package, so it gets a **sparse package** (packaged with external location), see below.
- **Code signing.** A sparse package must be signed with a certificate that the computer trusts. A self-signed certificate would need administrator rights to install, so the release needs a real code signing certificate (or a signing service), and CI signs `Erapkey.exe` and the package. This also helps with SmartScreen for the installer.

The first step is a prototype on Windows 11 that registers a sparse package and calls `WebAuthNPluginAddAuthenticator`; if a sparse package turns out not to be enough, the provider needs a full MSIX build, which Velopack does not make, and this channel is put on hold.

### Sparse package

`Erapkey.Desktop/Package/AppxManifest.xml`, packed by CI into `Erapkey.Identity.msix` (manifest and logos only, no binaries) and signed:

- `Identity` with the publisher of the signing certificate; `uap10:AllowExternalContent`; one `Application` with `Executable="Erapkey.exe"`, `uap10:TrustLevel="mediumIL"`, `uap10:RuntimeBehavior="win32App"`.
- `com:Extension Category="windows.comServer"` with `com:ExeServer Executable="Erapkey.exe" Arguments="-Embedding"` and `com:Class Id="<Erapkey plugin CLSID>"`.
- Capability `runFullTrust`.

`Erapkey.exe` gets an application manifest with `<msix publisher="…" packageName="…" applicationId="…"/>`, so a started `Erapkey.exe` from the install folder runs with the package identity.

Registration, in the Velopack install and update hooks (`Program.Main`), installed build only (not portable, not Debug):

1. `PackageManager.AddPackageByUriAsync(msix, new AddPackageOptions { ExternalLocationUri = <Velopack current\ folder> })`. `current\` keeps its path across updates.
2. Unregister in the uninstall hook: `WebAuthNPluginRemoveAuthenticator`, then `PackageManager.RemovePackageAsync`.

`Erapkey.Desktop` moves to a Windows SDK target (`net11.0-windows10.0.26100.0`) for `PackageManager`, or calls it through a small WinRT interop.

### Provider registration

On start, when **Windows passkey provider** is on and the process has package identity:

1. `WebAuthNPluginGetAuthenticatorState(clsid)`; if not registered, `WebAuthNPluginAddAuthenticator2` with:
   - name `Erapkey`, the CLSID, light and dark SVG logos (base64);
   - `authenticatorGetInfo` as CBOR: versions `FIDO_2_0`, `FIDO_2_1`; the AAGUID; options `rk: true`, `up: true`, `uv: true`; algorithms ES256;
   - no supported rpId list (all sites).
2. `WebAuthNPluginGetOperationSigningPublicKey` gives the key Windows signs requests with. It is read again on every start, not stored.
3. Until the user turns Erapkey on in **Windows Settings → Accounts → Passkeys → Advanced options**, the state is *disabled*. Erapkey's settings show the state and a button that opens that page (`ms-settings:` URI, to confirm on Windows 11).
4. `WebAuthNPluginRegisterStatusChangeCallback` updates the state shown in settings.

Turning the setting off calls `WebAuthNPluginRemoveAuthenticator` and clears the credential cache.

### COM server

`Erapkey.exe` itself is the COM server: the vault stays in one process and no second relay is needed.

- At startup (with the provider on) Erapkey calls `CoRegisterClassObject(clsid, factory, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE)`. When Erapkey is not running, Windows starts `Erapkey.exe -Embedding`: `Program.Main` treats it as a start to the tray without the main window, then registers the class object. The single-instance mutex and the interprocess window work as now.
- The class implements `IPluginAuthenticator` (from `pluginauthenticator.idl`): make credential, get assertion, cancel operation and lock status. The exact method list and structures are ported from the SDK headers; the interop uses `[GeneratedComInterface]` and `ComWrappers`, as Native AOT and trimming allow.
- Calls come on an MTA thread and are dispatched to the WPF dispatcher for windows, which are owned by the window handle in the request.
- **Every request's signature is checked** with the operation signing key before anything else; a request that fails is rejected and logged. Any process of the user can activate a COM class, so this check is what keeps them from asking Erapkey to sign.
- The request is CTAP2 CBOR. `WebAuthNDecodeMakeCredentialRequest` / `WebAuthNDecodeGetAssertionRequest` decode it; `PasskeyAuthenticator` does the work; `WebAuthNEncodeMakeCredentialResponse` / `WebAuthNEncodeGetAssertionResponse` encode the answer. Windows checks the origin against the rpId and gives only `clientDataHash`.
- Lock status: *locked* while the vault is locked. A request on a locked vault shows the unlock window (the same as a browser request).
- UV through `WebAuthNPluginPerformUserVerification` with the user name as the hint; consent and passkey choice in `PasskeyConfirmWindow`, in a shorter form when Windows already showed its own list.

### Credential cache

Windows lists a provider's passkeys in its dialog and in browser autofill from a cache of metadata that the provider fills: `WebAuthNPluginAuthenticatorAddCredentials`, `RemoveCredentials`, `RemoveAllCredentials`. Each item holds the credential ID, rpId, rp name, user handle, user name and display name, no key.

- Erapkey replaces the cache (remove all, add all live passkeys) after unlock and after every vault change that touches passkeys, including sync.
- The cache stays while the vault is locked, so Windows can still list the passkeys and start Erapkey, which then asks to unlock. The names of sites and accounts thus stay visible to Windows while Erapkey is locked; the setting **Show passkeys while locked** (on by default) clears the cache on lock instead.
- Turning the provider off or uninstalling clears it.

### Both channels at once

With the Windows provider enabled, the browser already shows Erapkey through Windows, and the extension would add a second dialog. So `status` reports `passkeys: "extension" | "windows" | "off"`, and the extension intercepts only when it is `extension`. Erapkey answers `windows` when the provider is registered and enabled in Windows; the user can force the extension in settings (for example, for a browser that does not use the Windows API).

## UI in Erapkey

- **Login details**: a *Passkey* row per live passkey: rpId, user name, created date, and a menu with **Remove** and **Move to another login**. No copy action.
- **Login editor**: passkeys are listed read-only with **Remove**; user name and display name are editable.
- List: a passkey icon on logins that have one; search matches `RpId` and `UserName`.
- **New item** does not create passkeys: they come only from a site.
- Removing a login with a passkey warns that the site may no longer let the user sign in with it.
- **Settings → Security → Passkeys**: *Save passkeys from the browser* (extension channel), *Windows passkey provider* with its state (not supported on this Windows / not registered / turned off in Windows → **Open Windows settings** / on), *Show passkeys while locked*.

## Security

- Private keys stay in the vault and in `Erapkey.exe`; signing happens there.
- The browser channel trusts the origin from `sender.origin` and checks the rpId in Erapkey; the page can affect neither. A compromised extension can ask for signatures only for the sites the user visits, after a confirmation in Erapkey, as with passwords.
- The Windows channel trusts Windows for the origin check and verifies the signature of every request.
- No silent operation: every create and get is a user action. Passkeys are not used while the vault is locked.
- UV is never claimed without a check.
- Passkeys are as safe as the vault: anyone with the master password (and the vault file) can sign in. This is the trade-off of synced passkeys and is the same for every password manager.

## Development

- The authenticator and the browser channel are tested on any Windows with [webauthn.io](https://webauthn.io) and the Debug instance (`Erapkey.Dev`, `com.erapkey.host.dev`).
- The Windows channel needs Windows 11 24H2 or later (a VM is enough) and a test signing certificate trusted on that machine. Microsoft's [Passkey Manager sample](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/passkeymanager/) is the reference for registration and the COM server. Debug builds use their own CLSID and package name, so they do not replace the installed provider.

## Implementation order

1. **Core**: `PasskeyData`, vault version 7, merge of passkeys, `PasskeyAuthenticator`, rpId rules, tests with Fido2NetLib.
2. **Desktop UI**: passkeys in login details and editor (view, remove, move), search, settings section.
3. **Browser channel**: requests and `features`, `PasskeyConfirmWindow`, UV through Windows Hello, the extension's MAIN world and relay scripts, fallback. Then conditional mediation with the inline menu.
4. **Code signing** in CI (certificate or signing service).
5. **Windows channel prototype** on Windows 11: sparse package and `WebAuthNPluginAddAuthenticator`. Go or no-go for the rest.
6. **Windows channel**: COM server, request signature check, credential cache, settings, both-channel switch.
7. README and [browser-extension.md](browser-extension.md) updated.

## Later

- `prf` / `hmac-secret`: a per-passkey random secret, so sites that encrypt with passkeys work.
- EdDSA (`-8`) through BouncyCastle, RS256 (`-257`).
- Import and export in the FIDO Credential Exchange Format (CXF); the stored fields above map to its passkey item.
- Firefox, together with step 7 of the browser extension.
- macOS and iOS providers (AuthenticationServices), reusing `PasskeyAuthenticator`.
- Hybrid (phone) sign-in, if Windows exposes it to plugins.

## Open questions

- Is a sparse package enough for `WebAuthNPluginAddAuthenticator`, and is `runFullTrust` the only capability needed? Answered by step 5.
- Which code signing option: an OV certificate on a hardware token or a cloud signing service, and which is available to an individual publisher.
- Whether the Chrome Web Store and Edge Add-ons accept the wider content script permission without extra review delays.
