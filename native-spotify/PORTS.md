# Standalone playback portability

The requirement is direct Spotify playback in HourNerd on Windows, iOS 16,
Android and LG webOS, with separate Pandora/Spotify accounts and no installed
Spotify app dependency. Existing Spotify Desktop/Spicetify controller builds do
not meet that requirement.

## Current implementation and evidence

Windows uses a pinned librespot native engine in a private child process. The
WPF host communicates through inherited stdin/stdout, opens Spotify's pairing
page in the user's browser, and protects the refresh token using Windows DPAPI.
The engine plays through its native audio backend. Its queue has no account-tier
check, skip counter, cooldown or time-based quota. Explicit ad flags and
non-music URIs are excluded. This does not change Spotify's entitlement or key
servers; it does not establish Free-account compatibility. Upstream currently
reports audio-key delivery failures for some accounts, including Premium users.

Source pinned at librespot commit
`15e02e86c72d96dd39d548e3749f00abe39ffdde`. Tests cover queue behavior and the host
protocol; only owner/device testing can establish real Spotify playback.

## Port requirements; these are NOT completed ports

| Platform | Host integration | Remaining validation |
| --- | --- | --- |
| Windows | WPF, child-process JSON, native audio, DPAPI | Owner sign-in, playlists/search and actual audio |
| iOS 16 / iPhone XR | Native engine library + Swift bridge; Keychain; AVAudioSession/PCM audio sink | arm64 device linking, background audio and live streams |
| Android | Native engine library + JNI; Keystore; AudioTrack/foreground playback service | ABI builds, audio focus/background handling and live streams |
| LG webOS | Native service/audio backend or compatible in-app streaming runtime | Exact TV model/webOS version, native execution/audio access and supported media pipeline |

The reusable Rust library currently contains queue/content-selection logic.
Authentication/network/playback still lives in the Rust process executable; it
needs extraction behind callback-based library APIs for mobile embedding.
A Windows child-process backend cannot simply be copied to iOS or to a packaged
webOS browser application.

Spotify's current official iOS and Android SDKs control its installed app, so
using those would reintroduce the dependency the user explicitly rejected.
The web playback SDK requires Premium and an appropriate protected-media
runtime; embedding a web page alone does not prove native/TV playback works.

No iOS, Android or LG Spotify playback binary has been produced by this change.
