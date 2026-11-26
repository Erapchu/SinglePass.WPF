# Application updates

Erapkey updates itself from [GitHub Releases](https://github.com/Erapchu/Erapkey/releases) using [Velopack](https://velopack.io). This document describes how releases are built, how the application finds and installs them, and where files go.

The code:

- `Erapkey/Program.cs` — entry point; runs Velopack hooks before WPF starts.
- `Erapkey/Services/UpdateService.cs` — checks, downloads and applies updates.
- `Erapkey/App.xaml.cs` — starts the update service after unlock and offers a restart.
- `Erapkey/Views/Controls/AboutControl.xaml` — **Settings → About → Updates** card.
- `.github/workflows/dotnet-desktop.yml` — builds, packs and publishes a release.

## Release contents

Each release contains:

| File | Purpose |
|---|---|
| `Erapkey.App-win-Setup.exe` | Installer for new users. |
| `Erapkey.App-win-Portable.zip` | Portable copy that also updates itself. |
| `Erapkey.App-X.Y.Z-full.nupkg` | Full application package. Downloaded when no delta applies. |
| `Erapkey.App-X.Y.Z-delta.nupkg` | Difference from the previous release. Absent in the first Velopack release. |
| `releases.win.json` | Update feed: versions, file names, sizes and SHA256 hashes. |

The application itself is published as a self-contained, single-file, ReadyToRun Windows x64 build.

## Installation

### Setup.exe

Setup.exe installs for the current user without administrator rights and without a wizard, then starts the application.

```
%LocalAppData%\Erapkey.App\
  Update.exe              Velopack updater
  current\                application files; replaced on update
  packages\               downloaded packages
```

It also creates Start menu and desktop shortcuts and an entry in **Apps & features** for uninstalling.

### Portable.zip

The user extracts the archive to any writable folder and runs `Erapkey.exe` in its root:

```
<folder>\
  Erapkey.exe          launcher; start this one
  Update.exe              Velopack updater
  current\                application files; replaced on update
  .portable               marks the copy as portable
```

Updates are applied in place, in the same folder. Nothing is written to the system: no shortcuts, no uninstall entry. Delete the folder to remove the application. The folder must be writable by the user; under `C:\Program Files` without administrator rights updates fail.

### User data

The vault and settings are **not** part of the installation. They stay in `%LocalAppData%\Erapkey` (see `Constants.LocalAppDataDirectoryPath`) for both Setup and Portable copies. Updating or uninstalling does not touch them, and a portable copy moved to another computer starts with an empty vault there.

The Velopack package id is `Erapkey.App` on purpose. Velopack installs into `%LocalAppData%\<packId>` and deletes that folder on uninstall; the id `Erapkey` would put the application into the data folder and delete the vault on uninstall. **Never change the package id to `Erapkey`.** Changing it at all also breaks updates for existing installations.

## How the application updates

1. **Startup.** `Program.Main` calls `VelopackApp.Build().Run()` before WPF starts. It handles install, update and uninstall hooks (the process exits for those) and applies an update downloaded in a previous session.
2. **Background check.** After the vault is unlocked for the first time, `UpdateService.Start` waits 30 seconds, checks for updates, then checks every 12 hours. Only published, non-prerelease releases are considered. Checks run only when **Check for updates automatically** is on (`CheckForUpdates` in `commonSettings.json`, on by default).
3. **Download.** When a newer version exists, it is downloaded in the background: the delta packages from the installed version if possible, otherwise the full package. Velopack verifies each package against the SHA256 in the feed.
4. **Prompt.** When the download finishes and the main window is open, the user is asked once per session whether to restart now. Until the update is installed, a dot is shown next to **Settings** in the navigation, the **About** card says "Update available: X", and **Restart and update** is available on the About page. The running application is never replaced: Erapkey usually stays in the notification area, so the update waits until the user restarts it.
5. **Apply.** On restart the application shuts down normally (clearing the clipboard as usual), Velopack replaces `current\` and starts the new version. The vault must be unlocked again. If the user declines, the update is applied at the next start.

Manual **Check for updates** on the About page runs steps 2–4 immediately.

A copy that was not installed by Velopack (for example, run from Visual Studio) cannot update itself. The About page says so and hides the update controls.

## Release workflow

The **Build and create release** workflow is started manually on `master`. After restore and publish it:

1. Gets the version from Nerdbank.GitVersioning (`version.json` plus git height), for example `1.11.5`, and the tag `v1.11.5`. If that release is already published, the remaining steps are skipped.
2. Installs the Velopack CLI (`vpk`), pinned to the same version as the `Velopack` NuGet package.
3. Downloads the latest release's full package with `vpk download github`, so the next step can build a delta. When there is no previous Velopack release, this step only prints a warning.
4. Runs `vpk pack` on the publish folder with package id `Erapkey.App`.
5. Uploads the files with `vpk upload github` into a draft release, generates release notes and publishes the release.

The release is visible to the application only after step 5.

When updating Velopack, change the `Velopack` package version in `Erapkey.csproj` and the `vpk` version in the workflow together.

## Delta size

A delta contains only the binary difference from the previous version. With an ordinary code change it is a few hundred kilobytes, both for the single-file build and for a build with separate files (measured locally: about 0.1 MB against an 80 MB full package). Expect a larger delta when the .NET runtime patch version changes, because the workflow installs the latest `10.0.x` SDK.

## Testing a release

1. Run the workflow and check that the release contains the files listed above.
2. Install Setup.exe (or extract Portable.zip). In **About → Updates**, **Check for updates** should report the latest version.
3. Push any commit to `master` and run the workflow again. The new release should contain a `-delta.nupkg`.
4. Open the installed application and unlock it. Within a minute it should offer to restart; or press **Check for updates**.
5. Accept. The application restarts, and **About** shows the new version.

To preview the "update ready" indicators in a Debug build without a release, set the environment variable `ERAPKEY_FAKE_UPDATE` to any version (for example `9.9.9`) before starting the application. **Restart and update** does nothing in this mode.

## Troubleshooting

- Application log: `%LocalAppData%\Erapkey\Logs`. `UpdateService` logs the installed version, downloads and errors.
- Velopack log: `velopack.log` in the installation folder (`%LocalAppData%\Erapkey.App` or the portable folder).
- GitHub API allows 60 unauthenticated requests per hour per IP address. Checks every 12 hours stay far below that.

## Limitations

- Binaries are not code-signed. Windows SmartScreen warns when Setup.exe is first run. Updates are downloaded by the application rather than a browser, so SmartScreen is not expected to prompt for them (not yet verified on a real release).
- Package hashes come from the same release as the packages, so they protect against corrupted downloads, not against a compromised repository or release.
