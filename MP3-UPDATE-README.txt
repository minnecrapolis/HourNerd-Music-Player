HourNerd Music Player - Pandora MP3 export test update

For an EXISTING working Windows HourNerd Music Player installation.

1. Extract the entire ZIP.
2. Close the player completely, including the tray icon.
3. Run Apply-MP3-Update.cmd.
4. Open HourNerd, play a Pandora song, and click Save MP3... at the top
   of Now Playing. Choose a destination folder. Playback continues.

The same command is also in the song menu. The selected song is captured
when you click Save; changing songs does not change the download target.

This update replaces the main EXE and adds two NAudio DLLs. It keeps the
existing audio repair and Pandora connection DLLs. It does not install a
new Pandora client configuration. A backup folder is created first.
To roll back: close HourNerd, restore HourNerdMusicPlayer.exe from that
backup, and remove NAudio.Core.dll/NAudio.Wasapi.dll if they were newly added.
Custom installation folder: run Apply-MP3-Update.ps1 -InstallPath 'YOUR FOLDER'.

AAC streams are converted to genuine MP3 at 192 kbps with Windows Media
Foundation; MP3 streams are preserved directly. Conversion does not increase
the quality of the source stream. Expired URLs, unavailable codecs, and
non-audio responses show a download error without changing playback.
Temporary/partial files are removed. Music is saved only when requested.

Conversion and decoding tests use generated audio, not Pandora content.
Live Pandora export on the owner's computer still needs verification.
Source and third-party notices are in the repository's mp3-export branch.
NAudio: Copyright Mark Heath and contributors, MIT license included.

This build also adds Buy the developer a beer in the sidebar, with Cash App
and Zelle instructions using the payment email shown in the app and a copy-email button.

