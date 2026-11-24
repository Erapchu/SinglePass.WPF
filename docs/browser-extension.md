# Browser extension (specification)

Status: **first version for Chrome and Edge** (steps 1–5 and the popup of step 6 of the [implementation order](#implementation-order)). This document describes how the SinglePass browser extension talks to the desktop application, what it may ask for and how the parts are installed. The pipe code shared with the SSH agent is described in [ssh-agent.md](ssh-agent.md).

Code: `SinglePass.Core/Services/Browser/` (protocol, pairing, session crypto), `SinglePass.Core/Services/WebsiteMatcher.cs` and `PublicSuffixList.cs`, `SinglePass.NativeHost/`, `SinglePass.WPF/Services/BrowserBackend.cs` and `NativeMessagingRegistration.cs`, `extension/`. Tests: `SinglePass.Core.Tests/BrowserPipeServerTests.cs`, `WebsiteMatcherTests.cs`, and `BrowserExtensionInteropTests.cs`, which runs the extension's TypeScript protocol code in Node against the C# server.

## Goals

- Suggest and fill logins in Chrome, Edge and Firefox (and other Chromium-based browsers) from the vault of the running SinglePass.
- Match entries by the address the browser reports for the tab, so a look-alike site gets no suggestions. The quick-access popup no longer reads the browser address bar (UI Automation could not tell the scheme, the right edit field or text being typed from the page address); it is off by default and stays for other applications.
- The vault never leaves the desktop application. The extension gets one entry at a time, after a user action, and keeps no secrets in browser storage.

Not in the first version: saving new logins from the browser, passkeys, cards and identities, filling without SinglePass running.

## Architecture

```
Extension (Manifest V3)
  background service worker ── chrome.runtime.connectNative("com.singlepass.host")
        │  Native Messaging: stdin/stdout, JSON
        ▼
SinglePass.NativeHost.exe        started by the browser, one per extension connection
        │  \\.\pipe\singlepass-browser-<user SID>, length-prefixed JSON
        ▼
SinglePass.exe                   BrowserPipeServer → unlocked vault, pairing and unlock windows
```

Browsers cannot open named pipes or local sockets; Native Messaging is the supported way for an extension to reach a local program. The browser starts the program listed in a registered manifest and only for the extension IDs listed there.

A localhost HTTP or WebSocket server is **not** used: every web page can send requests to `localhost`, which would need its own authentication and DNS rebinding protection.

### Components

| Component | Location | Role |
|---|---|---|
| Extension | `extension/` (new) | Detects login forms, shows suggestions, fills fields, holds the pairing key. TypeScript, bundled with esbuild. |
| Native host | `SinglePass.NativeHost/` (new project) | Relays messages between the browser and the pipe. Has no vault access and no logic. |
| Browser server | `SinglePass.WPF` + `SinglePass.Core` | Pipe server, pairing, session crypto, URL matching, answers. |

The native host is a separate executable, not `SinglePass.exe` with a flag: the browser starts the path from the manifest **without arguments of our choice** (Chrome passes the extension origin, Firefox the manifest path and extension ID), and the host must not run Velopack hooks, take the single-instance mutex or load WPF. It is a small console application published with Native AOT, so it starts quickly and needs no runtime.

### Separate pipe from the SSH agent

The browser server uses its own pipe, not the SSH agent's: the protocols differ (JSON vs the SSH agent binary protocol), the trust differs (the agent never hands out keys; the browser server returns passwords), and each can be turned off on its own. The pipe setup is shared: the code common to `SshAgentServer` and `BrowserPipeServer` (ACL, `FirstPipeInstance`, client process chain, accept loop) moves into a `NamedPipeServer` base class in `SinglePass.Core`.

## Native host registration

The manifest is written to `%LOCALAPPDATA%\SinglePass\browser\com.singlepass.host.<browser>.json` and registered under the current user (no administrator rights):

| Browser | Registry key (default value = manifest path) |
|---|---|
| Chrome, and Chromium-based browsers that read the Chrome key (Brave, Vivaldi) | `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.singlepass.host` |
| Edge | `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.singlepass.host` |
| Firefox | `HKCU\Software\Mozilla\NativeMessagingHosts\com.singlepass.host` |

Chromium manifest:

```json
{
  "name": "com.singlepass.host",
  "description": "SinglePass",
  "path": "C:\\Users\\<user>\\AppData\\Local\\SinglePass.WPF\\current\\SinglePass.NativeHost.exe",
  "type": "stdio",
  "allowed_origins": ["chrome-extension://<Chrome Web Store ID>/", "chrome-extension://<Edge Add-ons ID>/"]
}
```

Firefox uses `"allowed_extensions": ["singlepass@<domain>"]` instead of `allowed_origins`.

- The installed build registers in the Velopack install and update hooks (`Program.Main` → `VelopackApp`) and unregisters on uninstall. `current\` is the stable Velopack folder, so updates do not change the path.
- The portable build checks the registration on every start and rewrites it when the path changed.
- **Settings → Security → Browser extension** turns the integration off: unregisters the host and stops the pipe server.
- Debug builds register their own host name; see [Development](#development).

## Transport

### Browser ↔ native host

Native Messaging framing: a 32-bit length in native byte order (little-endian on Windows), then UTF-8 JSON. A message from the host to the browser is at most 1 MB. When the extension disconnects, the browser closes stdin and the host exits.

### Native host ↔ SinglePass

`\\.\pipe\singlepass-browser-<user SID>`, the same pipe security as the SSH agent: ACL for the current user only, network logons denied, `FirstPipeInstance`. Framing: 32-bit big-endian length, then UTF-8 JSON, at most 1 MB.

The host connects to the pipe once, when the browser starts it, and forwards every message unchanged in both directions. It cannot add an `id` to its own answers (after `hello` the messages are encrypted), so it reports problems with events:

- The pipe does not exist (SinglePass is not running or the integration is off): the host writes `{ "event": "app_not_running" }` and exits.
- SinglePass closes the pipe (exit, integration turned off): the host exits; the browser reports the port as disconnected.

The extension does not reconnect in a loop: it connects again (a new host process and a new `hello`) on the next user action, such as opening the toolbar popup or focusing a login field.

### Client check

For every pipe connection the server takes the client process chain (as the SSH agent does, see [ssh-agent.md](ssh-agent.md#confirmation)):

- the client must be `SinglePass.NativeHost.exe` from the application folder, otherwise the connection is closed and logged;
- the browser is the first process above it that is not `cmd.exe` (Chrome and Edge start native hosts through `cmd /d /s /c`). It is shown in the pairing window (`chrome.exe`, `msedge.exe`, …) and stored with the pairing, but not enforced: there are many Chromium-based browsers.

This check only keeps other programs from casually talking to the pipe: any program of the user can start `SinglePass.NativeHost.exe` itself. The pairing key below is what protects the answers.

## Pairing

The pipe ACL lets any process of the user connect, so the server also requires a key that only a paired extension holds.

Keys are ECDH on P-256: WebCrypto in all supported browsers and `ECDiffieHellman` in .NET both support it.

1. On install the extension generates a static key pair and keeps it in IndexedDB as a non-extractable `CryptoKey`. `chrome.storage` cannot hold it: it stores JSON only, so the private key would have to be exported.
2. `associate` sends the extension's static public key and a name (`Chrome`, `Edge`, …). SinglePass generates its own static key pair for this pairing.
3. Both sides show a six-digit code: the first 4 bytes of `SHA-256("singlepass-pair" || appPublic || extensionPublic)` as a big-endian number, modulo 1 000 000, padded with zeros. SinglePass shows a window: *Connect browser "Chrome" (chrome.exe)? Code 482 917*; the extension popup shows the same code.
4. The user confirms in SinglePass. The pairing is stored and the extension receives the pairing ID and the app's static public key.

Pairings are per device and are not synced. They are stored in `%LOCALAPPDATA%\SinglePass\browserPairings.dat`, protected with DPAPI for the current user (like the cloud tokens, see `ProtectedTokenHolder`):

```jsonc
{ "Id": "<guid>", "Name": "Chrome", "Browser": "chrome.exe",
  "ExtensionPublicKey": "<base64 SPKI>", "AppPrivateKey": "<base64 PKCS#8>",
  "CreatedAt": "<UTC>", "LastUsedAt": "<UTC>" }
```

**Settings → Security → Browser extension** lists pairings with their last use and removes them. A removed pairing makes the extension ask for pairing again.

Pairing works while the vault is locked: it reveals nothing about the vault.

## Session

Each connection starts with `hello`, which authenticates both sides and creates session keys:

1. Extension → `hello { pairingId, ephemeralKey, nonce }` (in plain JSON).
2. SinglePass → `hello { ephemeralKey, nonce }`, or error `not_paired`.
3. Both compute
   `secret = ECDH(appStatic, extensionStatic) || ECDH(appEphemeral, extensionEphemeral)`
   `keys = HKDF-SHA256(secret, salt = nonceExt || nonceApp, info = "singlepass-browser-v1")` → 32 bytes extension→app, 32 bytes app→extension.
4. Every further message is `{ "n": <counter>, "c": "<base64 AES-256-GCM ciphertext>" }`. The nonce is the 64-bit counter of its direction, padded to 12 bytes; the counter must increase by one, otherwise the session is closed.

Without the static private key of a paired extension a process cannot derive the session keys, so it gets nothing. The static key sits in the browser profile; a program running as the same user can read it, as it can read the memory of the unlocked SinglePass. The pairing protects against other programs simply connecting to the pipe, not against malware that already owns the user account.

## Requests

Inside the session, requests and responses are JSON objects with an `id` chosen by the extension:

```jsonc
// request
{ "id": 7, "type": "getLogins", "url": "https://login.example.com/signin" }
// response
{ "id": 7, "ok": true, "logins": [ ... ] }
// error
{ "id": 7, "ok": false, "error": "locked" }
```

| Request | Answer | Notes |
|---|---|---|
| `status` | `{ locked, version }` | |
| `unlock` | `{ locked }` | Shows the unlock window (as an SSH agent request does) and answers when the vault is unlocked, or with `locked: true` after a minute. |
| `getLogins { url }` | `logins: [{ entryId, name, login, match }]` | Logins whose site matches `url` (see below), best match first. `match` is `exact` or `domain`. **No passwords.** |
| `getCredentials { entryId, url }` | `{ login, password, totp? }` | Only if the entry still matches `url`; otherwise `not_found`. `totp` is the current code when the entry has TOTP. |
| `getTotp { entryId, url }` | `{ code, validFor }` | Same check as above. |
| `openEntry { entryId }` | `{}` | Brings SinglePass to the front with the entry selected. |

Events pushed by SinglePass without a request: `{ "event": "locked" }`, `{ "event": "unlocked" }`, `{ "event": "vaultChanged" }` (defined, not sent yet: the popup asks again each time it opens). On `locked` the extension drops everything it showed. The native host sends `{ "event": "app_not_running" }` in plain JSON (see [Transport](#native-host--singlepass)).

Errors: `locked`, `not_paired`, `not_found`, `bad_request`, `unsupported_version`, `internal`.

Rules:

- There is no request that lists the vault or returns entries without a URL. Even a compromised extension can get only the logins of the sites the user visits.
- `getCredentials` repeats the URL check: an entry ID alone is not enough.
- No confirmation window per fill: the click in the browser is the user's consent. A setting may add one later.
- `version` in `hello` and `status` is the protocol version (`1`). A newer extension with an older SinglePass gets `unsupported_version` and asks the user to update.

## URL matching

`SinglePass.Core/Services/WebsiteMatcher.cs` (new). The entry's site comes from `Login.SiteField`, parsed as `LoginViewModel.ParseWebsite` does now (a missing scheme means `https`).

1. Normalize both hosts: lowercase, IDN to punycode, no trailing dot.
2. **Exact**: same host (and same port, if the entry has one).
3. **Domain**: same registrable domain by the [Public Suffix List](https://publicsuffix.org/) (eTLD+1): `login.example.com` matches an entry for `example.com`, but `example.co.uk` does not match `other.co.uk`, and `alice.github.io` does not match `bob.github.io`. The list is `SinglePass.Core/Resources/public_suffix_list.dat`, an embedded resource read by `PublicSuffixList` (normal, wildcard `*.` and exception `!` rules, ICANN and private sections alike); a host that no rule covers uses the implicit `*` rule (its last label is the suffix). The file is updated with releases.
4. IP addresses and single-label hosts (`localhost`, `router`) match only exactly.
5. An entry saved with `https` is not offered on an `http` page (downgrade). An `http` entry is offered on `https`.
6. Only `http` and `https` pages; never `file:`, `chrome:`, `about:` or extension pages.

## Extension

Manifest V3, TypeScript bundled with esbuild (`extension/`, `npm run build` → `extension/dist/`). The first version targets Chrome and Edge, which share one manifest. Firefox comes later with its own manifest: it runs MV3 background code as `background.scripts` (an event page), not as a service worker, and needs `browser_specific_settings.gecko.id`.

- **Background service worker**: the native port, pairing, session crypto, request queue. An open native messaging port keeps the service worker alive in current browsers; when the worker or the port goes away, it connects again (new `hello`) on the next user action.
- **Toolbar popup**: status (not running / locked / unlocked), unlock button, logins for the current tab, pairing code during pairing. Choosing a login fills it into the tab (`chrome.scripting.executeScript` with `activeTab`, `src/fill.ts`); the buttons beside it copy the login or the password. The fill checks that the tab is still on the origin of the address the login was matched with.
- **Content script** (not yet): finds login forms (password fields, username fields before them, also in same-origin iframes), shows an inline SinglePass icon in the field, fills on the user's choice.

Filling rules:

- Fill only after a user action: choosing a login in the inline menu or the popup, or the keyboard shortcut. Never on page load, so hidden forms cannot collect passwords.
- Fill only visible, enabled fields of the top-level page or same-origin frames. A cross-origin frame is matched by its own URL, not by the page's.
- Fire `input` and `change` events so that page scripts notice the values.
- Passwords stay in memory only for the fill; nothing from the vault goes to `chrome.storage`.

## Settings in SinglePass

**Settings → Security → Browser extension**:

- *Browser integration* on/off — registers or unregisters the native host and starts or stops the pipe server. Off by default until the extension is published.
- Paired browsers: name, browser, last use, **Remove**.

## Development

Debug builds of SinglePass are a separate instance, so testing the extension never touches the working vault and can run next to the installed application:

| | Release | Debug |
|---|---|---|
| Data folder (vault, settings, logs, pairings) | `%LOCALAPPDATA%\SinglePass` | `%LOCALAPPDATA%\SinglePass.Dev` |
| Pipes | `singlepass-ssh-agent-<SID>`, `singlepass-browser-<SID>` | the same with `-dev` |
| Single-instance mutex, interprocess window | as now | with `-dev` |
| Native host name | `com.singlepass.host` | `com.singlepass.host.dev` |

`Constants.IsDevBuild` (the `DEBUG` symbol of `SinglePass.Core`) switches all of them. The Debug build registers `com.singlepass.host.dev` for the `SinglePass.NativeHost.exe` next to it in `bin\Debug`, with the ID of the unpacked extension in `allowed_origins`. The development extension (`npm run build:dev`) connects to `com.singlepass.host.dev`; its manifest has a fixed `key`, so Chrome and Edge give the unpacked extension the same ID on every machine.

To try it: build the solution in Debug, start SinglePass (it creates a new empty vault in `SinglePass.Dev`), turn on **Settings → Security → Browser extension**, then in `extension/` run `npm install` once and `npm start`.

Scripts in `extension/` (Node 22):

| Script | What it does |
|---|---|
| `npm start` | Builds the development extension, starts Chrome (or Edge when Chrome is not installed) with it loaded and rebuilds and reloads it on every change under `src/`. Pass a browser or pages: `npm start -- edge https://github.com/login`. |
| `npm run start:edge`, `npm run start:chrome` | The same in that browser. |
| `npm run start:release` | The same against the installed (Release) SinglePass: `com.singlepass.host` and its vault. |
| `npm run watch` | Development build that rebuilds on changes, for an extension loaded by hand. |
| `npm run build:dev` | Development build into `dist/` (`com.singlepass.host.dev`, fixed key). |
| `npm run build` | Release build into `dist/` (`com.singlepass.host`, no key: the store assigns the ID). |
| `npm run typecheck` | TypeScript check. |
| `npm run test:pipe` | The Node pipe client of `BrowserExtensionInteropTests`. |

`npm start` uses its own browser profile in `extension/.profile/<browser>-<dev|release>`, so the pairing stays between runs and the everyday profile is untouched; close that window before starting it again. Branded Chrome ignores `--load-extension` since version 137, so `run.mjs` loads the folder through the DevTools protocol (`Extensions.loadUnpacked` over `--remote-debugging-pipe` with `--enable-unsafe-extension-debugging`); Edge also gets `--edge-skip-compat-layer-relaunch`, without which it restarts itself in a process that has no DevTools pipe. A reload after a rebuild restarts the service worker, so reopen the popup to see the change.

To load it by hand instead: `npm run build:dev`, open `chrome://extensions` (or `edge://extensions`), turn on developer mode and load `extension/dist/`.

## Implementation order

1. Debug instance separation (above).
2. `SinglePass.Core`: `PublicSuffixList` and `WebsiteMatcher` with tests.
3. `NamedPipeServer` base extracted from `SshAgentServer`; `BrowserPipeServer` with the protocol, pairing and session crypto, with tests (a test client plays the extension).
4. `SinglePass.NativeHost` (Native AOT), registration in Velopack hooks, for the portable build and for Debug, packaging.
5. Pairing window and **Settings → Security → Browser extension** in `SinglePass.WPF`.
6. The extension for Chrome and Edge: background worker and popup first (list and copy), then the content script and inline fill.
7. Firefox: its manifest and registration.
8. Store listings (Chrome Web Store, Edge Add-ons, Firefox AMO); their extension IDs go into the manifests.
