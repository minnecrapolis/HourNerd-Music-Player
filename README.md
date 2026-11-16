# HourNerd Music Player

A Windows desktop Pandora player maintained by **Chris Dahlkvist / HourNerd**,
based on **Elpis** by Adam Haile and its contributors.

## Windows downloads — release paused

**No public EXE or MSI release is available yet.**

The intended experience is simple: install the application, launch it, and sign
in with your normal **Pandora email and password**. Users must not need to obtain
partner passwords or protocol keys.

The current development source has no bundled Pandora integration settings.
The application compiles and its 22 offline regression checks pass, but this
build cannot authenticate to Pandora without distributor configuration. It is
not ready for end-user installation. Automatic public release is disabled until
normal sign-in is restored and both installer formats pass validation.

When ready, this page will include direct **Setup.exe** and **Setup.msi** links,
requirements, step-by-step installation instructions, updates and troubleshooting.
See [Releases](https://github.com/minnecrapolis/HourNerd-Music-Player/releases)
for published packages. Source files and draft attachments are not installers.

## Planned installation experience

1. Download **Setup.exe** for the installation wizard, or **Setup.msi** for native Windows Installer deployment. Choose one format per computer.
2. Close any running copy of the player, including its system tray icon.
3. Run the installer and follow its prompts.
4. Open **HourNerd Music Player** from the Start menu.
5. Sign in with your own Pandora email and password, then choose a station.

Windows with the **Microsoft .NET Framework 4.8.1 Runtime**, internet access and
a Pandora account in a supported region are required. Recipients will not need
Visual Studio, PowerShell or a source-code ZIP. These steps describe the release
goal; the current development build is not ready for this use.

Close the player before updating. Use the same installer format for updates;
uninstall before switching between EXE and MSI. Per-user settings are retained.

## Development

Requires Windows and Visual Studio 2022 with the .NET desktop development
workload. Microsoft .NET Framework 4.8.1 build references and dependencies restore
through NuGet. Open `HourNerdMusicPlayer.sln`, or run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-HourNerdMusicPlayer.ps1 -Test
```

The application and audio engine target x86. The output executable is
`HourNerdMusicPlayer\bin\Release\HourNerdMusicPlayer.exe`; keep its supporting
files together. The offline tests use deliberately invalid fixture values and
do not sign in to a live Pandora account.

`Build-Windows-Installers.ps1` builds the EXE and native MSI from a validated
public distribution. The Windows workflow builds and tests changes on `main`,
but its public publication step is paused. The EXE and MSI installers now build successfully, including MSI administrative
extraction and per-file hash verification. The successful packaging run is
[available here](https://github.com/minnecrapolis/HourNerd-Music-Player/actions/runs/43948271913).
A successful compilation is not a substitute for hands-on playback testing.

## Features and testing status

A persistent station sidebar, artwork and playback details, station management,
history, and Dark/Light/Midnight/Forest themes are included in the development
source. Spotify support is not included.

Intermittent exits during station changes were previously reported and have not
been confirmed resolved. Report reproducible issues under
[Issues](https://github.com/minnecrapolis/HourNerd-Music-Player/issues). Do not
attach account settings, authentication tokens or connection values.

## Credits and licenses

- **Chris Dahlkvist** — HourNerd Music Player project lead and maintainer.
- **Alex “Signal”** — Development contributor.
- **Adam Haile and contributors** — Original Elpis application.
- **Un4seen Developments / radio42** — BASS / BASS.NET audio technology.

See `AUTHORS.md`, `LICENSE` and `THIRD-PARTY-NOTICES.txt`. Personal payment
contact information is not bundled; tipping is disabled until a verified public
payment link is configured. HourNerd Music Player is independent and is not
affiliated with or endorsed by Pandora.
