HourNerd Music Player - Standalone Spotify Windows test 1.2.0

1. Extract the ENTIRE ZIP to a new folder.
2. Close HourNerd completely, including its tray icon.
3. Run Apply-MP3-Update.cmd and verify it reports the installed EXE path.
4. Run Start-HourNerd-Standalone.cmd to open the updated installed copy.
5. Choose Spotify, click Sign in to Spotify, approve it in your browser,
   and return to HourNerd to select a playlist or search result.

This update uses native playback. It does not install or control Spotify
Desktop. The old bridge's setup tools are removed from the HourNerd installation
folder after being backed up. It does not uninstall or modify Spotify/Spicetify.
The installed Pandora and repaired audio DLLs are preserved.

See Spotify\SPOTIFY-README.txt for account, queue, compatibility and test details.
The native player needs 64-bit Windows. Pandora keeps its existing login.

Rollback: close HourNerd, restore the EXE/DLLs and Spotify subfolder from the
newest MP3-update-backup folder. Keep that folder until testing is complete.
