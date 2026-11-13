# SinglePass.WPF

SinglePass is a Windows password manager for storing credentials and other information in a local encrypted file. It also provides a quick-access popup for filling credentials in other applications and optional Google Drive synchronization.

## Features

- Create, edit, delete, search and sort credentials. Each entry can contain a name, login, password, website, notes and additional fields.
- Copy credentials to the clipboard, show or hide passwords, and open saved websites in a browser.
- Search by name, login, website or notes, including transliterated text.
- Open a quick-access popup with a configurable global hotkey. The popup can suggest entries based on the current browser address and paste a login and password or individual fields.
- Synchronize or upload credentials to Google Drive after signing in.
- Change the master password, choose a theme and select English, Russian or the system language.
- Access the application from the Windows notification area.

## Getting started

Download the Windows x64 archive from [Releases](https://github.com/Erapchu/SinglePass.WPF/releases), extract it and run the application. The release workflow publishes a self-contained build.

On first launch, enter a master password of at least eight characters. Use the same password to open the local credential store on subsequent launches. Keep a secure backup of both the password and the credential file: the password is needed to decrypt the stored data.

### Quick-access popup

1. Add a credential and fill in its website address.
2. Set a popup hotkey in **Settings**.
3. Open the website in a Chromium-based browser, focus the login field and press the configured hotkey.
4. Select an entry in the popup to paste the credentials. You can also search or paste individual fields.

The popup uses the clipboard and simulates keyboard input. Filling both fields assumes that pressing Tab moves focus from the login field to the password field; behavior depends on the target application and page.

### Google Drive

Open **Settings → Google Drive** and sign in to enable cloud synchronization. The cloud page provides separate actions to synchronize and upload credentials. An internet connection and a Google account are required.

## Data and configuration

Application data is stored under `%LOCALAPPDATA%\SinglePass`:

- `singlePass.dat` — encrypted credentials.
- `commonSettings.json` — application settings.
- `googleDrive.json` — Google Drive authorization data.
- `Cache` — cached data; logs are stored in `Logs`.

The credential file is encrypted using the master password. Treat backups and authorization data as sensitive, and use a strong, unique master password.

## Build from source

The project targets .NET 10 and Windows x64. Building requires Windows, the .NET 10 SDK and Visual Studio/MSBuild with the .NET desktop development workload; the project uses a Windows UI Automation COM reference.

From a Visual Studio Developer PowerShell at the repository root:

```powershell
msbuild .\SinglePass.WPF.sln /t:Restore /p:Configuration=Release
msbuild .\SinglePass.WPF.sln /t:Build /p:Configuration=Release
```

To publish a self-contained Windows x64 build, as in the release workflow:

```powershell
msbuild .\SinglePass.WPF\SinglePass.WPF.csproj /t:Publish /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishReadyToRun=true /p:PublishSingleFile=true
```

The output is in `SinglePass.WPF\bin\Release\win-x64\publish`.

## Screenshots

![Alt text](web/images/main.png "SinglePass main window")

![Alt text](web/images/settings.png "SinglePass settings")

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
