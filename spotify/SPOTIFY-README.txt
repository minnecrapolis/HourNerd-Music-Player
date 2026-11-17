HourNerd Spotify - Windows desktop test build

Install/update HourNerd using the included Apply-MP3-Update.cmd first.

1. Install the Spotify desktop application and sign in normally.
2. Open HourNerd. Choose Spotify in the new Pandora / Spotify dropdown.
3. Click Set up Spotify. It installs the bundled local bridge and ad-blocking
   extension through Spicetify, backing up its configuration first.
4. Leave Spotify open. Return to HourNerd, choose My playlists or Search,
   select an entry, and click Play selected (or double-click it).

HourNerd supplies the interface; Spotify Desktop handles music playback in
its normal authenticated session. The separate Spotify window can be minimized.
No Spotify developer account, Client ID, or pasted access token is needed.

Settings has separate Pandora account and Spotify account sections. Pandora
uses its own saved login. To change Spotify accounts, use Open Spotify / change
account, sign out from Spotify's profile menu and sign in to the desired account.
Switching accounts in one service does not change the other service's login.
HourNerd does not ask for or store your Spotify password.

Controls: previous, play/pause, next, like, your playlists, song/playlist search.
HourNerd adds no skip counter. A server-side refusal is still possible and is
not silently overridden. The bundled adblockify extension blocks client ad
components; host-read sponsorship inside a podcast recording is not reliably
identified or removed. No claim of guaranteed ad blocking is made before a
live Spotify test on your machine.

The local bridge listens ONLY on 127.0.0.1:48372. It requires a per-user random
key and accepts only Spotify's application origin. It exchanges song metadata,
playlist/search results and specific playback commands. It never exports your
Spotify password, session cookies or access token to HourNerd.

Switching to Spotify pauses Pandora. Switching back pauses Spotify; choose
Play to resume Pandora. Spotify playlists and search results also appear in the left sidebar, using
the same layout as Pandora's station browser. Existing Pandora downloads and the beer tip button are preserved.

Spotify updates may require re-running setup or a compatible Spicetify build.
The setup checks Spicetify's compatibility result rather than forcing patches.
To disable these extensions, close HourNerd, run Remove-Spotify-Bridge.cmd,
and restart Spotify. It preserves other Spicetify extensions.

Pinned tools / source:
Spicetify CLI 2.46.7, https://github.com/spicetify/cli (Apache-2.0).
adblockify, https://github.com/rxri/spicetify-extensions (MIT).
Pinned adblock source commit: 5da6cf1bb723f9efd0177f5fa4f4673b9e0c0936.
Licenses are included. Full HourNerd source is in the repository's mp3-export
branch. Actual Spotify sign-in, library availability, audio, ads, and repeated
skips still need the owner's account/device test. Build tests use fixtures.
