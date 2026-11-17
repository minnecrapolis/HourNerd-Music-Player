using System.Text.RegularExpressions;
namespace HourNerdMusicPlayer.Spotify
{
    public sealed class SpotifySnapshot
    {
        public bool connected { get; set; }
        public bool playing { get; set; }
        public string account { get; set; }
        public string title { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public string artwork { get; set; }
        public double position { get; set; }
        public double duration { get; set; }
        public string error { get; set; }
        public SpotifyEntry[] entries { get; set; }
    }
    public sealed class SpotifyEntry
    {
        public string Name { get; set; }
        public string detail { get; set; }
        public string uri { get; set; }
        public byte[] ArtImage { get { return null; } }
        public override string ToString() { return Name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail); }
    }
    internal static class SpotifyUris
    {
        internal static bool Valid(string value)
        { return value != null && Regex.IsMatch(value,"\\Aspotify:(track|album|playlist):[A-Za-z0-9]{22}\\z"); }
    }
}
