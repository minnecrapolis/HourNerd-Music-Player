// HourNerd local Spotify bridge. GPL-3.0-or-later.
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HourNerdMusicPlayer.Spotify
{
    internal sealed class SpotifySnapshot
    {
        public bool playing { get; set; }
        public bool adblockLoaded { get; set; }
        public string title { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public string artwork { get; set; }
        public double position { get; set; }
        public double duration { get; set; }
        public string error { get; set; }
        public SpotifyEntry[] entries { get; set; }
    }
    internal sealed class SpotifyEntry
    {
        public string Name { get; set; }
        public string detail { get; set; }
        public string uri { get; set; }
        public byte[] ArtImage { get { return null; } }
        public override string ToString() { return Name + (string.IsNullOrEmpty(detail) ? "" : " — " + detail); }
    }
    internal sealed class SpotifyBridge : IDisposable
    {
        internal const int DefaultPort = 48372;
        private readonly object gate = new object();
        private readonly Queue<PendingCommand> commands = new Queue<PendingCommand>();
        private readonly TcpListener listener;
        private readonly string token;
        private readonly int port;
        private readonly SemaphoreSlim slots = new SemaphoreSlim(6);
        private bool disposed;
        private SpotifySnapshot snapshot = new SpotifySnapshot();
        private DateTime lastSeen = DateTime.MinValue;
        private sealed class PendingCommand { public DateTime Created; public string name; public string value; }
        internal SpotifyBridge(string key, int port = DefaultPort)
        {
            if (key == null || key.Length != 64) throw new ArgumentException("Invalid bridge key");
            this.token = key; this.port = port;
            listener = new TcpListener(IPAddress.Loopback, port); listener.Start();
            Task.Run((Action)Accept);
        }
        internal static string ReadOrCreateKey()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HourNerd Music Player", "SpotifyBridge");
            Directory.CreateDirectory(folder); string file = Path.Combine(folder, "bridge.key");
            if (File.Exists(file)) { string existing = File.ReadAllText(file).Trim(); if (System.Text.RegularExpressions.Regex.IsMatch(existing, "\\A[a-f0-9]{64}\\z")) return existing; }
            byte[] bytes = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            string key = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            File.WriteAllText(file, key, new UTF8Encoding(false)); return key;
        }
        internal bool Connected { get { lock (gate) return DateTime.UtcNow - lastSeen < TimeSpan.FromSeconds(8); } }
        internal SpotifySnapshot Snapshot { get { lock (gate) return snapshot; } }
        internal bool Command(string name, string value = "")
        {
            if (!new HashSet<string> { "play", "pause", "toggle", "next", "previous", "heart", "library", "search", "playUri" }.Contains(name)) return false;
            if (value == null || value.Length > 200) return false;
            if (name == "playUri" && !ValidUri(value)) return false;
            lock (gate)
            {
                if (DateTime.UtcNow - lastSeen > TimeSpan.FromSeconds(8) || commands.Count >= 20) return false;
                commands.Enqueue(new PendingCommand { name = name, value = value, Created = DateTime.UtcNow }); return true;
            }
        }
        internal static bool ValidUri(string value)
        {
            return value == "spotify:collection:tracks" || value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "\\Aspotify:(track|album|playlist|artist):[A-Za-z0-9]{22}\\z");
        }
        private async void Accept()
        {
            while (!disposed)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); } catch { break; }
                if (!slots.Wait(0)) { client.Close(); continue; }
                Task.Run(async () => { try { await Handle(client).ConfigureAwait(false); } catch { } finally { client.Close(); slots.Release(); } });
            }
        }
        private async Task Handle(TcpClient client)
        {
            client.ReceiveTimeout = 3000; client.SendTimeout = 3000;
            using (var timeout = new CancellationTokenSource(4000))
            using (var registration = timeout.Token.Register(() => client.Close()))
            {
                var stream = client.GetStream(); var header = new MemoryStream(); byte[] one = new byte[1];
                while (header.Length < 8192)
                {
                    if (await stream.ReadAsync(one, 0, 1, timeout.Token).ConfigureAwait(false) == 0) return;
                    header.WriteByte(one[0]);
                    byte[] data = header.GetBuffer(); int n = (int)header.Length;
                    if (n >= 4 && data[n - 4] == 13 && data[n - 3] == 10 && data[n - 2] == 13 && data[n - 1] == 10) break;
                }
                if (header.Length >= 8192) { await Reply(stream, 400, "{}", null, timeout.Token); return; }
                string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                string[] first = lines[0].Split(' '); if (first.Length != 3) return;
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++) { int colon = lines[i].IndexOf(':'); if (colon > 0) { string k = lines[i].Substring(0, colon); if (fields.ContainsKey(k)) { await Reply(stream, 400, "{}", null, timeout.Token); return; } fields[k] = lines[i].Substring(colon + 1).Trim(); } }
                string host, origin; fields.TryGetValue("Host", out host); fields.TryGetValue("Origin", out origin);
                bool allowedOrigin = origin == "https://xpui.app.spotify.com" || origin == "https://zlink.app.spotify.com";
                if (host != "127.0.0.1:" + port || !allowedOrigin || first[1] != "/exchange") { await Reply(stream, 403, "{}", null, timeout.Token); return; }
                if (first[0] == "OPTIONS") { await Reply(stream, 204, "", origin, timeout.Token); return; }
                string supplied; fields.TryGetValue("X-HourNerd-Bridge", out supplied);
                int length; string rawLength; fields.TryGetValue("Content-Length", out rawLength);
                if (first[0] != "POST" || !EqualKey(supplied, token)) { await Reply(stream, 403, "{}", origin, timeout.Token); return; }
                if (fields.ContainsKey("Transfer-Encoding") || !int.TryParse(rawLength, out length) || length < 2 || length > 262144) { await Reply(stream, 400, "{}", origin, timeout.Token); return; }
                byte[] body = new byte[length]; int read = 0;
                while (read < length) { int count = await stream.ReadAsync(body, read, length - read, timeout.Token).ConfigureAwait(false); if (count == 0) return; read += count; }
                SpotifySnapshot update = null;
                var json = new JavaScriptSerializer { MaxJsonLength = 262144, RecursionLimit = 12 };
                try { update = json.Deserialize<SpotifySnapshot>(Encoding.UTF8.GetString(body)); } catch { }
                if (update == null) { await Reply(stream, 400, "{}", origin, timeout.Token); return; }
                var outgoing = new List<object>();
                lock (gate)
                {
                    snapshot = update; lastSeen = DateTime.UtcNow;
                    while (commands.Count > 0) { var command = commands.Dequeue(); if (lastSeen - command.Created <= TimeSpan.FromSeconds(8)) outgoing.Add(new { command.name, command.value }); }
                }
                await Reply(stream, 200, json.Serialize(new { commands = outgoing }), origin, timeout.Token).ConfigureAwait(false);
            }
        }
        private static bool EqualKey(string a, string b) { if (a == null || a.Length != b.Length) return false; int difference = 0; for (int i = 0; i < a.Length; i++) difference |= a[i] ^ b[i]; return difference == 0; }
        private static async Task Reply(NetworkStream stream, int code, string body, string origin, CancellationToken cancellation)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            string headers = "HTTP/1.1 " + code + " Response\r\nConnection: close\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nContent-Length: " + bytes.Length + "\r\n";
            if (origin != null) headers += "Access-Control-Allow-Origin: " + origin + "\r\nVary: Origin\r\nAccess-Control-Allow-Methods: POST, OPTIONS\r\nAccess-Control-Allow-Headers: content-type, x-hournerd-bridge\r\nAccess-Control-Allow-Private-Network: true\r\n";
            byte[] start = Encoding.ASCII.GetBytes(headers + "\r\n"); await stream.WriteAsync(start, 0, start.Length, cancellation).ConfigureAwait(false); if (bytes.Length > 0) await stream.WriteAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false);
        }
        public void Dispose() { disposed = true; listener.Stop(); lock (gate) commands.Clear(); }
    }
}
