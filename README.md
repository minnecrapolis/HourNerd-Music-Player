# HourNerd Music Player 1.1.3

A Windows desktop Pandora player rebuilt and maintained by **Chris Dahlkvist / HourNerd**.

Stations stay visible in a searchable left sidebar. Select a station to listen; the right side shows playback, artwork and song details. History, station management, station creation, Settings, About and Sign out remain accessible from the sidebar.

## Download and install (Windows)

**Initial prerelease:** playback and station switching still need hands-on Windows testing. Use the release assets below; GitHub's green **Code** button downloads source code, not an installer.

| Download | Choose this when |
| --- | --- |
| [Download Setup.exe](https://github.com/minnecrapolis/HourNerd-Music-Player/releases/download/v1.1.3/HourNerd-Music-Player-1.1.3-Setup.exe) | You want the normal installation wizard (recommended). |
| [Download Setup.msi](https://github.com/minnecrapolis/HourNerd-Music-Player/releases/download/v1.1.3/HourNerd-Music-Player-1.1.3-Setup.msi) | You prefer the native Windows Installer package or managed deployment. |
| [Release page and checksums](https://github.com/minnecrapolis/HourNerd-Music-Player/releases/tag/v1.1.3) | Read release notes, verify SHA256 checksums, or download corresponding source. |

Both installers include the same application and supporting files. Install **one format per computer**. You do not need Visual Studio, PowerShell, or the source ZIP to use the installed player.

### Requirements

- Windows 10 version 20H2 or later, or Windows 11, with [Microsoft .NET Framework 4.8.1 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481). Install the **Runtime**, then restart if Windows requests it.
- Internet access and your own Pandora account in a region where Pandora operates.
- Pandora **partner connection settings** (connection password, incoming key, outgoing key) supplied separately by your integration provider or administrator. It is **not** your Pandora account password. Public downloads do not include this value. Without all three values, the application can be installed but cannot sign in or play music.

### Installation steps

1. Download **Setup.exe** above, or choose **Setup.msi** if you prefer that format.
2. Close any running copy of HourNerd Music Player, including its tray icon.
3. Run your chosen installer and follow its prompts. These prerelease installers are unsigned; check that your download came from this repository and verify its checksum if Windows asks about the publisher. If Windows blocks it, follow your organization's software policy.
4. Launch **HourNerd Music Player** from the Start menu.
5. On the sign-in page, select **Pandora connection setup**. Enter the partner connection password, incoming key, and outgoing key supplied separately by your provider or administrator, then select **Save connection**. It is encrypted using Windows protection for your current account and stored only on this computer.
6. Enter your **personal Pandora email and password** in the regular sign-in fields, then select **Login**. Choose a station from the sidebar.

Connection setup is needed once per Windows account/computer, or when the supplied value changes. It is never packaged into the public application, source ZIP, or installers. The saved connection file is `%LOCALAPPDATA%\HourNerdMusicPlayer\Connection\PandoraPartner.dat`; copying it to another computer/account does not transfer access.

### Updating or removing

Close the player before running a newer installer. Use the same installer format when updating. To switch between EXE and MSI, uninstall the previous format first using **Settings > Apps**, then install the new format. Saved per-user settings are retained; a fresh Windows account needs its own connection setup.

### Troubleshooting

| Problem | What to do |
| --- | --- |
| .NET runtime missing | Install the .NET Framework 4.8.1 **Runtime** linked above. |
| Connection setup required | Open **Pandora connection setup**, save the separately supplied connection password and both keys, then sign in. |
| Invalid partner login | Ask your integration provider/administrator to verify the supplied connection value. Changing your personal Pandora password will not fix a partner login failure. |
| Invalid personal login | Check your own Pandora account email/password and confirm you can sign in to Pandora. |
| Connection setup was saved but is no longer readable | Save it again under your current Windows account. Windows-encrypted settings cannot be moved between accounts/computers. |
| The app exits while switching stations | This is a known prerelease issue under investigation. Report the steps under [Issues](https://github.com/minnecrapolis/HourNerd-Music-Player/issues); do not attach account settings, connection values, or authentication tokens. |

Advanced/developer use: `HOURNERD_PANDORA_PARTNER_PASSWORD`, `HOURNERD_PANDORA_INCOMING_KEY`, and `HOURNERD_PANDORA_OUTGOING_KEY` can supply local process environment overrides. All three settings are required. Never commit it, put it in a workflow, or share it in an issue. Saving through the setup dialog clears an override in the running application; remove persistent environment overrides if they should no longer apply on later launches.

---

## Build and run

Close the old player, extract this project into its own folder, and open PowerShell there:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-HourNerdMusicPlayer.ps1 -Test -Run
```

Requires Windows, Visual Studio 2022 with .NET desktop development, and Internet access to restore NuGet packages. Microsoft .NET Framework 4.8.1 build references are restored through NuGet. The output is `HourNerdMusicPlayer\bin\Release\HourNerdMusicPlayer.exe`; keep its supporting files together.

Open `HourNerdMusicPlayer.sln` for development. The audio engine remains x86.

## Build the public download

Run on the Windows build computer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-HourNerdMusicPlayer.ps1
```

This builds and tests the app, strips debug symbols and build paths, stages an explicit list of runtime files, adds a public-profile marker, includes licenses and corresponding source, and produces `dist/HourNerd-Music-Player-1.1.0-Windows.zip`.

Share that Windows ZIP. Recipients extract it and run `HourNerdMusicPlayer.exe`; they do not need Visual Studio. The .NET Framework 4.8.1 runtime is required.

The public package starts with its own per-Windows-user profile. It does not import a development/legacy profile and contains no live account credentials, tokens, stations, histories, diagnostic logs or caches. Public author credits and the expressly requested Zelle tipping address remain. The publisher rejects debug files and developer-machine paths in the compiled runtime. It does not read `%APPDATA%` while packaging.

## Appearance

Settings > Appearance offers **Dark**, **Light**, **Midnight**, and **Forest**. Selection previews immediately; Save persists it. Cancelling or leaving Settings without saving restores the saved theme.

## Optional tips

The tipping action is disabled in this public build until a verified public payment link is configured. No personal payment email is bundled.

## Credits

- **Chris Dahlkvist** — Project lead and maintainer; HourNerd Music Player rebuild and branding.
- **Alex “Signal”** — Development contributor.
- **Adam Haile** — Original creator of **Elpis**, the project this application is based on.
- **Alexey Seliverstov, ƝƠƇƬƲƦƝƛԼ, and the Elpis community** — Original contributions, as credited upstream.

Original project: <https://github.com/adammhaile/Elpis>

Original copyright notices and the GPL license text are preserved. Modified source files carry a 2026 modification notice. The `upstream` folder retains original project documents for attribution and history. See `AUTHORS.md` and `LICENSE`.

## Existing settings

In development mode, on first start the player copies an existing `%APPDATA%\Elpis\elpis.config` profile into `%APPDATA%\HourNerd Music Player\hournerdmusicplayer.config`, translating product-specific keys while retaining encrypted credentials and preferences. It leaves the original profile intact and never overwrites an existing HourNerd profile. Public packages skip this import and use `%APPDATA%\HourNerd Music Player\Public` instead.

## Diagnostics

Start with `Start-HourNerdMusicPlayer-Diagnostics.ps1`, inspect timing lines with `Show-HourNerdMusicPlayer-Timings.ps1`, and inspect crash information with `Show-HourNerdMusicPlayer-Crash.ps1`. These helpers keep reports on your computer.

## Validation

The previous 1.0.0 interface was user-confirmed working. The 1.1.0 changes include theme persistence, public profile isolation, a release packager and tipping UI. Static source/resource, palette contrast and packaging-rule checks are performed here. This environment cannot compile or execute Windows WPF, so the new Windows build, 19-test suite and public ZIP generation must run on the Windows build computer. No precompiled 1.1.0 executable is claimed in this source archive.

This is an independent client, not affiliated with or endorsed by Pandora. Bundled third-party libraries retain their original notices and terms.


## Single-file Windows installer (1.1.3)

Install Inno Setup 6 on the build machine once (`winget install --id JRSoftware.InnoSetup -e -s winget -i`), then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Windows-Installer.ps1
```

This performs a fresh public build and regression run, validates the public payload hashes, and compiles `dist/HourNerd-Music-Player-1.1.3-Setup.exe`. Recipients need only that EXE and the .NET Framework 4.8.1 runtime. Setup creates the Installed Apps entry, Start menu and optional desktop shortcuts, and its own uninstaller. Existing profile data is preserved. No account credentials are packaged.

The application normalizes its working directory before initialization, and generated shortcuts and the installer finish-page launch use the same per-user folder. This removes dependence on a development folder. The exact cause of the intermittent installed-copy freeze is not yet established; the new launch path needs the Windows focus-switching test described in START-HERE.txt. No compiled or Windows-tested Setup.exe is included in this source ZIP. Spotify remains unfinished and is outside this installer update.


## Native MSI packaging

`Build-Windows-MSI.ps1` creates a real Windows Installer database with an embedded cabinet, native welcome/finish dialogs, shortcuts, per-user registration, upgrade identity and uninstall support. It packages the existing validated 1.1.3 public runtime ZIP; recipients run no PowerShell scripts. The build uses Windows Installer Automation and MakeCab on the build computer, followed by administrative extraction and per-file hash verification.

`Build-Windows-Installers.ps1 -UseExistingDistribution` rebuilds both EXE and MSI from the existing public ZIP. Omit the switch to rebuild the app and public ZIP too. These are build-computer commands, not recipient installation steps. The .NET Framework 4.8.1 runtime is a recipient prerequisite. Choose one installer format per computer; switching formats requires uninstalling the current installation, preserving the user profile. The native MSI has not been executed in this non-Windows workspace.
