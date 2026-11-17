HourNerd Spotify - STANDALONE WINDOWS TEST 1.2.0

Spotify audio plays through the bundled native hournerd-spotify.exe engine.
Spotify Desktop and Spicetify are not needed. You do not need to uninstall
Spotify if it is already installed; HourNerd does not use or modify it.

1. Choose Spotify in HourNerd's service dropdown.
2. Click Sign in to Spotify. Approve the sign-in in your browser using your
   Spotify account, which can be different from your Pandora account.
3. Return to HourNerd, choose My playlists or Search, and play a selection.
4. Keep HourNerd open for playback. No separate Spotify app is required.

Settings has separate account controls. Sign in/change account reconnects
Spotify with a new browser sign-in. Sign out of Spotify clears its saved token.
Spotify's saved refresh token is encrypted for the current Windows user using
DPAPI; no Spotify password, plain-text token cache, or local control port is used.

The queue adds no skip quota or cooldown. It accepts music tracks, filters
explicit ad metadata, and does not load ad/episode URIs. It cannot identify ads
spoken inside a normal music recording. A queue ending is not a skip limit.
Choose another playlist or search result when it ends.

IMPORTANT TEST STATUS:
This is an experimental native playback build for testing with the owner's
Premium account. No claim is made that it provides Free-account streaming.
Spotify controls account authorization and audio-key delivery. Librespot reports
that some accounts cannot receive audio keys; those accounts may fail playback.
A failure stops playback and displays an error instead of repeatedly requesting
streams. Automated fixtures do not prove live account playback.

The shared queue/protocol is designed for later ports. The iOS, Android and LG
webOS native playback ports are NOT included or verified in this Windows ZIP.
TV native service/audio feasibility still needs validation on the owner's TV.

Pinned native dependency: librespot 0.8.0 development source
https://github.com/librespot-org/librespot
Commit: 15e02e86c72d96dd39d548e3749f00abe39ffdde
MIT license included. HourNerd modifications are GPL-3.0-or-later.
Source: https://github.com/minnecrapolis/HourNerd-Music-Player/tree/spotify-standalone
