# HourNerd Music Player

## Experimental standalone Spotify — Windows 1.2.0 test

[Download the standalone Spotify update ZIP](https://github.com/minnecrapolis/HourNerd-Music-Player/actions/runs/44057158131/artifacts/14316628020). This is an update for an existing working HourNerd installation, not a stable installer release. The test artifact expires after 14 days.

1. Close HourNerd completely, including its tray icon. Keep Spotify Desktop closed for this test.
2. Extract the entire ZIP into a new folder and run `Apply-MP3-Update.cmd`.
3. Confirm the updater reports version 1.2.0.0 and your installed EXE path, then run `Start-HourNerd-Standalone.cmd`.
4. Select Spotify, click **Sign in to Spotify**, approve the sign-in in your browser, then return to HourNerd to choose My playlists or Search.

Audio uses a bundled native engine. Spotify Desktop and Spicetify are not required or modified. Pandora keeps its own login, and the updater preserves the installed Pandora/audio-repair DLLs and existing tip configuration. Spotify's refresh token is protected with Windows account encryption.

The music queue adds no skip quota or cooldown and filters explicitly identified ad/non-music entries. It does not identify sponsorship embedded in a recording or grant streaming access denied by Spotify. This experiment targets the owner's Premium account; Free-account playback is not claimed. Upstream reports audio-key delivery failures for some accounts, so real account playback still needs owner testing.

[Windows build and automated checks passed](https://github.com/minnecrapolis/HourNerd-Music-Player/actions/runs/44057158131): native compilation; queue/ad-filter fixtures; startup/command/shutdown protocol; Pandora/audio lifecycle regressions; MP3 checks; native WPF account/service-switching fixtures; update preservation and corrupt-package rejection. These are not live Spotify audio tests.

iOS 16, Android and LG webOS Spotify ports are not yet built. See [port status](native-spotify/PORTS.md) and [source review](https://github.com/minnecrapolis/HourNerd-Music-Player/pull/2).


A Windows desktop Pandora player maintained by **Chris Dahlkvist / HourNerd**,
based on **Elpis** by Adam Haile and its contributors.

## Credits and licenses

- **Chris Dahlkvist** — HourNerd Music Player project lead and maintainer.
- **Alex “Signal”** — Development contributor.
- **Adam Haile and contributors** — Original Elpis application.
- **Un4seen Developments / radio42** — BASS / BASS.NET audio technology.
