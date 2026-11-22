# SSH agent

SinglePass serves the SSH key entries of the vault to `ssh`, `git`, `scp` and other OpenSSH clients, the way `ssh-agent`, 1Password or Bitwarden do. Programs get signatures, never the keys.

Code:

- `SinglePass.Core/Services/SshAgent.cs` — the agent protocol (draft-miller-ssh-agent): list keys, sign.
- `SinglePass.Core/Services/SshAgentServer.cs` — the named pipe and the program that connects to it.
- `SinglePass.Core/Services/SshSigningKey.cs` — reads private keys and signs: Ed25519, RSA (`ssh-rsa`, `rsa-sha2-256`, `rsa-sha2-512`), ECDSA on NIST P-256/384/521.
- `SinglePass.Core/Services/BcryptPbkdf.cs` — the key derivation of OpenSSH keys with a passphrase.
- `SinglePass.WPF/Services/SshAgentKeyStore.cs` — keys of the unlocked vault, confirmation, unlock on request.

Tests are in `SinglePass.Core.Tests/SshAgentTests.cs`, with keys made by `ssh-keygen`.

## Pipe

`\\.\pipe\singlepass-ssh-agent-<user SID>`. It has its own name, so it does not conflict with the Windows `ssh-agent` service (`\\.\pipe\openssh-ssh-agent`), and the SID keeps users of one machine apart: pipe names are shared by all sessions.

- The pipe ACL allows the current user and denies network logons. Only processes of this user on this machine can connect.
- The first instance is created with `FirstPipeInstance`. If another program already holds the name, the agent does not start (and logs it) instead of sharing the name with it.
- The agent starts after the first unlock and runs until SinglePass exits. **Settings → Security → SSH agent** turns it off and shows the address.

Clients find it in one of two ways:

| Where | Value |
|---|---|
| `SSH_AUTH_SOCK` environment variable (all programs) | `\\.\pipe\singlepass-ssh-agent-<SID>` |
| `IdentityAgent` option of `ssh` / `ssh_config` | `//./pipe/singlepass-ssh-agent-<SID>` |

`IdentityAgent` needs forward slashes: the ssh option parser treats backslashes as escapes. Windows accepts both forms.

Only OpenSSH for Windows (`C:\Windows\System32\OpenSSH`) talks to named pipes. The `ssh` of Git for Windows (MSYS) expects a Unix socket and cannot use the agent.

## Requests

| Request | Answer |
|---|---|
| List keys (11) | SSH key entries of the vault (not in the trash) whose public key can be read, with the entry name as the comment. Nothing while the vault is locked. |
| Sign (13) | Asks the user, then signs with the key. Failure if the key is unknown, denied, or cannot be read. |
| Anything else | Failure. Keys cannot be added or removed through the agent, and extensions such as `session-bind@openssh.com` are not supported (clients go on without them). |

The public key of an entry comes from its private key when that can be read without the passphrase (OpenSSH keys always, unencrypted PEM keys), otherwise from the entry's public key field. Before signing, the decrypted key is checked against the requested public key.

## Locked vault

A request while the vault is locked shows the unlock window (at most every 30 seconds) and waits up to a minute for the unlock. Then the request goes on as usual, or gets no keys.

## Confirmation

Every signature is confirmed in a window on top of the others. It shows the key and the program, with the programs that started it, for example `ssh.exe ← git.exe ← Code.exe`, and the path of the first one.

- Enter and Escape deny, so a keystroke meant for another window cannot allow a request. Without an answer the request is denied after a minute.
- **Allow this program until the vault is locked** remembers the key together with the whole program chain (paths). The approvals end when the vault is locked.
- One question at a time; requests wait for it.

## Hosts

A host entry (`HostData`) keeps the address, port, user and a link to an SSH key entry. **Copy SSH command** builds:

```
ssh -o IdentityAgent=//./pipe/singlepass-ssh-agent-<SID> -o IdentitiesOnly=yes -i "%LOCALAPPDATA%\SinglePass\ssh\<key id>.pub" -p <port> user@host
```

The public key is written to `%LOCALAPPDATA%\SinglePass\ssh\<key id>.pub` (public keys are not secret). With `-i` pointing to it and `IdentitiesOnly=yes`, ssh offers only that key and has the agent sign with it. Without a linked key, or with the agent turned off, the command is plain `ssh -p <port> user@host`.

## Limits

- While the vault is unlocked, a program running as the same user could read the memory of SinglePass. The confirmation protects against silent use through the agent, not against that.
- `ForwardAgent` lets the remote server use the agent while the session lasts. Its requests come through the local `ssh.exe`, so a remembered approval for that ssh covers them too. Do not forward the agent to servers you do not trust.
- Hardware-backed keys (`sk-ssh-ed25519`, `sk-ecdsa`) and DSA keys are not supported.
