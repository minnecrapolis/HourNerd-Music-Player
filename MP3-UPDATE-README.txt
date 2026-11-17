HourNerd Music Player - Windows Spotify test update

1. Extract the entire ZIP.
2. Close HourNerd completely, including its tray icon.
3. Run Apply-MP3-Update.cmd. It backs up your existing EXE and adds the Spotify
   setup files. The installed Pandora and audio repair DLLs are preserved.
4. Open HourNerd and choose Spotify in the dropdown above your station list.
5. Click Set up Spotify once, with the Spotify desktop app already installed
   and signed in. Leave Spotify running for audio, then use HourNerd.

See Spotify\SPOTIFY-README.txt for details, dependencies and removal.
Pandora and Save MP3 remain available by switching the dropdown to Pandora.
The beer tip button is preserved in the private build.

The Spotify integration is a test build. Automated bridge and playback-command
fixtures passed; live account playback, ad blocking and repeated skips still
need testing. Spotify server restrictions and ads embedded inside recordings
are not guaranteed to be removable. HourNerd adds no local skip quota.

To roll back: close HourNerd, restore HourNerdMusicPlayer.exe from the newest
MP3-update-backup folder in the installation directory. Disable the Spotify
extensions with Spotify\Remove-Spotify-Bridge.cmd if desired.
