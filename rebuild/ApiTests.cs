// Offline regression tests using the real compiled PandoraSharp implementation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using PandoraSharp;
using PandoraSharp.Exceptions;
using Util;

internal class FakePandora : Pandora
{
    public readonly Queue<string> Responses = new Queue<string>();
    public readonly List<string> Urls = new List<string>();
    public readonly List<string> Bodies = new List<string>();
    protected override string RPCRequest(string url, string data)
    {
        Urls.Add(url);
        Bodies.Add(data);
        if (Responses.Count == 0) throw new Exception("Unexpected extra API request");
        return Responses.Dequeue();
    }
    public JSONResult Read() { return CallRPC("user.getStationList", new JObject()); }
    public JSONResult LoginCall() { return CallRPC("auth.userLogin", new JObject(), true, true); }
    public void AddLogin()
    {
        Responses.Enqueue(new JObject(
            new JProperty("stat", "ok"), new JProperty("result", new JObject(
                new JProperty("syncTime", Crypto.in_key.Encrypt("00001700000000")),
                new JProperty("partnerId", "42"),
                new JProperty("partnerAuthToken", "partner-test-token")))).ToString());
        Responses.Enqueue("{\"stat\":\"ok\",\"result\":{\"userId\":\"123\",\"userAuthToken\":\"user-test-token\",\"hasAudioAds\":true}}");
    }
    public bool Authorizing
    {
        get { return (bool)typeof(Pandora).GetField("_authorizing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this); }
    }
}

internal class RealTransport : Pandora
{
    public string Send(string url) { return base.RPCRequest(url, "{}"); }
}

internal class ApiTests
{
    private const string Expired = "{\"stat\":\"fail\",\"code\":1001,\"message\":\"expired\"}";
    private const string Success = "{\"stat\":\"ok\",\"result\":{\"stations\":[],\"marker\":\"fresh\"}}";
    private static int passed;
    private static int failed;
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Check(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    private static void Expect(ErrorCodes code, Action action)
    {
        try { action(); }
        catch (PandoraException e) { Assert(e.Fault == code, "Wrong error: " + e.Fault); return; }
        throw new Exception("Expected " + code);
    }
    private static void HttpStatus(int status, ErrorCodes expected)
    {
        // TcpListener avoids HttpListener URL reservations and administrator rights.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Exception serverError = null;
        var thread = new Thread(delegate()
        {
            try
            {
                using (var client = listener.AcceptTcpClient())
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 5000;
                    var header = new StringBuilder();
                    int value;
                    while ((value = stream.ReadByte()) >= 0)
                    {
                        header.Append((char)value);
                        if (header.ToString().EndsWith("\r\n\r\n")) break;
                    }
                    Assert(header.ToString().StartsWith("POST "), "Wrong HTTP method");
                    Assert(header.ToString().Contains("charset=utf-8"), "Missing UTF-8 content type");
                    // The test sends exactly two body bytes: {}.
                    Assert(stream.ReadByte() == '{' && stream.ReadByte() == '}', "Wrong request body");
                    byte[] reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Test\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
                    stream.Write(reply, 0, reply.Length);
                }
                listener.Stop(); // Any blind replay would fail instead of silently succeeding.
            }
            catch (Exception e) { serverError = e; }
        });
        thread.IsBackground = true;
        thread.Start();
        try
        {
            var transport = new RealTransport();
            string url = "http://127.0.0.1:" + port + "/";
            if (expected == ErrorCodes.SUCCESS) Assert(transport.Send(url) == "{}", "Response changed");
            else Expect(expected, delegate { transport.Send(url); });
            Assert(thread.Join(6000), "Mock server did not finish");
            if (serverError != null) throw serverError;
        }
        finally { listener.Stop(); }
    }

    private static void ArtworkDoesNotBlock()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var release = new ManualResetEvent(false);
        var changed = new ManualResetEvent(false);
        Exception serverError = null;
        var thread = new Thread(delegate()
        {
            try
            {
                using (var client = listener.AcceptTcpClient())
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 5000;
                    var header = new StringBuilder();
                    int value;
                    while ((value = stream.ReadByte()) >= 0)
                    {
                        header.Append((char)value);
                        if (header.ToString().EndsWith("\r\n\r\n")) break;
                    }
                    release.WaitOne(3000);
                    byte[] reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 3\r\nConnection: close\r\n\r\nart");
                    stream.Write(reply, 0, reply.Length);
                }
            }
            catch (Exception e) { serverError = e; }
        });
        thread.IsBackground = true;
        thread.Start();
        try
        {
            var data = JObject.Parse("{\"songName\":\"Test\",\"songRating\":0,\"audioUrlMap\":{\"highQuality\":{\"audioUrl\":\"https://example.invalid/audio\"}}}");
            data["albumArtUrl"] = "http://127.0.0.1:" + port + "/art";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var song = new Song(new Pandora(), data);
            Assert(watch.ElapsedMilliseconds < 1000, "Song construction waited for artwork");
            song.PropertyChanged += delegate(object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "AlbumImage") changed.Set();
            };
            release.Set();
            Assert(changed.WaitOne(6000), "Artwork completion was not reported to bindings");
            Assert(Encoding.ASCII.GetString(song.AlbumImage) == "art", "Artwork bytes changed");
            Assert(thread.Join(6000), "Artwork server did not finish");
            if (serverError != null) throw serverError;
        }
        finally { release.Set(); listener.Stop(); }
    }
    public static int Main()
    {
        Environment.SetEnvironmentVariable("HOURNERD_PANDORA_INCOMING_KEY", "offline-incoming-test-key");
        Environment.SetEnvironmentVariable("HOURNERD_PANDORA_OUTGOING_KEY", "offline-outgoing-test-key");
        // Deliberately invalid test values: no live credentials or network login.
        Environment.SetEnvironmentVariable("HOURNERD_PANDORA_PARTNER_PASSWORD", "offline-test-only");
        Check("local connection value is sent in the partner request", delegate
        {
            var p = new FakePandora(); p.AddLogin(); p.AuthenticateUser();
            Assert((string)JObject.Parse(p.Bodies[0])["password"] == "offline-test-only", "Local configuration was ignored");
        });
        Check("missing connection setup sends no authentication request", delegate
        {
            Environment.SetEnvironmentVariable("HOURNERD_PANDORA_PARTNER_PASSWORD", " ");
            try
            {
                var p = new FakePandora();
                Expect(ErrorCodes.PARTNER_CONFIGURATION_REQUIRED, delegate { p.AuthenticateUser(); });
                Assert(p.Urls.Count == 0, "An unconfigured client sent a request");
                Assert(!p.Authorizing, "Authorization state was not cleared");
            }
            finally { Environment.SetEnvironmentVariable("HOURNERD_PANDORA_PARTNER_PASSWORD", "offline-test-only"); }
        });
        Check("session renewal returns the new response", delegate
        {
            var p = new FakePandora(); p.Responses.Enqueue(Expired); p.AddLogin(); p.Responses.Enqueue(Success);
            Assert((string)p.Read().Result["marker"] == "fresh", "Returned stale fault");
            Assert(p.Urls.Count == 4, "Unexpected request count");
            Assert(p.Urls[3].Contains("user-test-token"), "Retry did not use new token");
        });
        Check("session renewal is bounded to one attempt", delegate
        {
            var p = new FakePandora(); p.Responses.Enqueue(Expired); p.AddLogin(); p.Responses.Enqueue(Expired);
            Expect(ErrorCodes.INVALID_AUTH_TOKEN, delegate { p.Read(); });
            Assert(p.Urls.Count == 4, "Recursive renewal");
        });
        Check("login auth faults never recursively renew", delegate
        {
            var p = new FakePandora(); p.Responses.Enqueue(Expired);
            Expect(ErrorCodes.INVALID_AUTH_TOKEN, delegate { p.LoginCall(); });
            Assert(p.Urls.Count == 1, "Recursive login");
        });
        Check("failed partner login preserves the error and clears state", delegate
        {
            var p = new FakePandora(); p.Responses.Enqueue("{\"stat\":\"fail\",\"code\":1002}");
            Expect(ErrorCodes.INVALID_PARTNER_LOGIN, delegate { p.AuthenticateUser(); });
            Assert(!p.Authorizing, "Login stuck in progress");
            p.AddLogin(); Assert(p.AuthenticateUser(), "Second login failed");
        });
        Check("failed user login preserves the error and clears state", delegate
        {
            var p = new FakePandora();
            // Obtain one valid partner response from another fixture.
            var fixture = new FakePandora(); fixture.AddLogin();
            p.Responses.Clear(); p.Responses.Enqueue(fixture.Responses.Dequeue());
            p.Responses.Enqueue("{\"stat\":\"fail\",\"code\":1012}");
            Expect(ErrorCodes.INVALID_PASSWORD, delegate { p.AuthenticateUser(); });
            Assert(!p.Authorizing, "Login stuck in progress");
        });
        Check("all production API requests use HTTPS", delegate
        {
            var p = new FakePandora(); p.ForceSSL = false; p.Responses.Enqueue(Success); p.Read();
            Assert(p.ForceSSL && p.Urls[0].StartsWith("https://"), "HTTP downgrade allowed");
        });
        Check("HTML is an invalid API response", delegate { Expect(ErrorCodes.API_INVALID_RESPONSE, delegate { new JSONResult("<html>blocked</html>"); }); });
        Check("missing status is an invalid API response", delegate { Expect(ErrorCodes.API_INVALID_RESPONSE, delegate { new JSONResult("{}"); }); });
        Check("missing fault code is an invalid API response", delegate { Expect(ErrorCodes.API_INVALID_RESPONSE, delegate { new JSONResult("{\"stat\":\"fail\"}"); }); });
        Check("mutation success without a result is accepted", delegate { Assert(!new JSONResult("{\"stat\":\"ok\"}").Fault, "Valid mutation rejected"); });
        Check("new server error codes remain visible", delegate
        {
            var p = new FakePandora(); p.Responses.Enqueue("{\"stat\":\"fail\",\"code\":1215}");
            Expect((ErrorCodes)1215, delegate { p.Read(); });
            Assert(Errors.GetErrorMessage((ErrorCodes)1215).Contains("1215"), "Code hidden from user");
        });
        Check("malformed login response clears state", delegate
        {
            var p = new FakePandora(); p.Responses.Enqueue("{\"stat\":\"ok\",\"result\":{}}");
            Expect(ErrorCodes.API_INVALID_RESPONSE, delegate { p.AuthenticateUser(); });
            Assert(!p.Authorizing, "Malformed response left login in progress");
        });
        Check("real HTTP 200 transport", delegate { HttpStatus(200, ErrorCodes.SUCCESS); });
        Check("real HTTP 403 classification", delegate { HttpStatus(403, ErrorCodes.API_ACCESS_DENIED); });
        Check("real HTTP 429 classification", delegate { HttpStatus(429, ErrorCodes.API_RATE_LIMITED); });
        Check("slow artwork does not block song construction and notifies bindings", ArtworkDoesNotBlock);
        Check("logout discards authentication tokens", delegate
        {
            var p = new FakePandora(); p.AddLogin(); p.AuthenticateUser(); p.Logout();
            p.Responses.Enqueue(Success); p.Read();
            Assert(!p.Urls[p.Urls.Count - 1].Contains("auth_token"), "Logged-out request retained a token");
        });
        Check("brand migration preserves source and encrypted credentials", delegate
        {
            string root = Path.Combine(Path.GetTempPath(), "HourNerdMigration-" + Guid.NewGuid().ToString("N"));
            string oldFolder = Path.Combine(root, "Elpis");
            string newFolder = Path.Combine(root, "HourNerd Music Player");
            try
            {
                Directory.CreateDirectory(oldFolder);
                string source = Path.Combine(oldFolder, "elpis.config");
                string data = "Elpis_Volume|72\r\nLogin_Password|encrypted-value-unchanged\r\nDebug_Logpath|" + oldFolder;
                File.WriteAllText(source, data, Encoding.Unicode);
                Assert(HourNerdMusicPlayer.ProfileMigration.CopyLegacyProfile(oldFolder, newFolder,
                    "elpis.config", "hournerdmusicplayer.config"), "Profile not copied");
                string migrated = File.ReadAllText(Path.Combine(newFolder, "hournerdmusicplayer.config"), Encoding.Unicode);
                Assert(File.ReadAllText(source, Encoding.Unicode) == data, "Original profile changed");
                Assert(migrated.Contains("HourNerdMusicPlayer_Volume|72"), "Volume key not migrated");
                Assert(migrated.Contains("Login_Password|encrypted-value-unchanged"), "Encrypted credential changed");
                Assert(migrated.Contains("Debug_Logpath|" + newFolder), "Log path not migrated");
                File.WriteAllText(Path.Combine(newFolder, "hournerdmusicplayer.config"), "new-settings", Encoding.Unicode);
                Assert(!HourNerdMusicPlayer.ProfileMigration.CopyLegacyProfile(oldFolder, newFolder,
                    "elpis.config", "hournerdmusicplayer.config"), "Existing profile overwritten");
                Assert(File.ReadAllText(Path.Combine(newFolder, "hournerdmusicplayer.config"), Encoding.Unicode) == "new-settings", "New settings lost");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
        Check("public marker selects an isolated per-user profile", delegate
        {
            string marker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HourNerdMusicPlayer.DistributionPolicy.PublicMarker);
            byte[] original = File.Exists(marker) ? File.ReadAllBytes(marker) : null;
            try
            {
                if (File.Exists(marker)) File.Delete(marker);
                string development = HourNerdMusicPlayer.DistributionPolicy.ProfileDirectory;
                Assert(!HourNerdMusicPlayer.DistributionPolicy.IsPublicDistribution, "Development mode incorrectly marked public");
                File.WriteAllText(marker, "public test");
                Assert(HourNerdMusicPlayer.DistributionPolicy.IsPublicDistribution, "Public marker ignored");
                string publicProfile = HourNerdMusicPlayer.DistributionPolicy.ProfileDirectory;
                Assert(publicProfile != development && Path.GetDirectoryName(publicProfile) == development,
                    "Public package would reuse the development profile");
                Assert(Path.GetFileName(publicProfile) == "Public", "Wrong public profile directory");
            }
            finally
            {
                if (original == null) File.Delete(marker);
                else File.WriteAllBytes(marker, original);
            }
        });
        Check("working folder does not depend on launch directory", delegate
        {
            string original = Environment.CurrentDirectory;
            string temporary = Path.Combine(Path.GetTempPath(), "hournerd-launch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                Environment.CurrentDirectory = temporary;
                string expected = HourNerdMusicPlayer.DistributionPolicy.WorkingDirectory;
                Assert(Path.IsPathRooted(expected), "Working folder must be absolute");
                HourNerdMusicPlayer.DistributionPolicy.ConfigureWorkingDirectory();
                Assert(Environment.CurrentDirectory == expected, "Startup did not normalize working folder");
                Assert(expected != AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                    "Startup is using the installation directory");
                Environment.CurrentDirectory = Path.GetTempPath();
                HourNerdMusicPlayer.DistributionPolicy.ConfigureWorkingDirectory();
                Assert(Environment.CurrentDirectory == expected, "Different launcher selected a different folder");
            }
            finally
            {
                Environment.CurrentDirectory = original;
                Directory.Delete(temporary, true);
            }
        });
        Console.WriteLine(passed + " passed; " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
}
