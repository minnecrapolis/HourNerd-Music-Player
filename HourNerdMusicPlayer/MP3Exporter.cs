// HourNerd, GPL-3.0-or-later. NAudio is used under its MIT license.
using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace HourNerdMusicPlayer
{
    internal static class MP3Exporter
    {
        internal static string SafeName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            value = value.Trim().TrimEnd('.');
            if (value.Length == 0) return "Pandora song";
            return value.Substring(0, Math.Min(110, value.Length));
        }
        internal static async Task SaveAsync(string audioUrl, string destination, IProgress<string> progress, CancellationToken cancellation, HttpMessageHandler transport = null)
        {
            Uri uri;
            if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new InvalidOperationException("This song does not have a downloadable audio stream.");
            string input = Path.Combine(Path.GetTempPath(), "HourNerd-audio-" + Guid.NewGuid().ToString("N"));
            string partial = Path.Combine(Path.GetDirectoryName(destination), ".HourNerd-" + Guid.NewGuid().ToString("N") + ".mp3");
            try
            {
                await DownloadAsync(uri, input, progress, cancellation, transport).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                progress.Report("Preparing MP3…");
                string typed = input + DetectExtension(input);
                File.Move(input, typed); input = typed;
                await Task.Run(() => ConvertFile(input, partial, cancellation), cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (File.Exists(destination)) File.Replace(partial, destination, null);
                else File.Move(partial, destination);
                progress.Report("MP3 saved");
            }
            finally
            {
                try { if (File.Exists(input)) File.Delete(input); } catch { }
                try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            }
        }
        internal static async Task DownloadAsync(Uri uri, string input, IProgress<string> progress, CancellationToken cancellation, HttpMessageHandler transport = null)
        {
            var urls = new Queue<Uri>();
            if (uri.Scheme == "http") { var secure = new UriBuilder(uri) { Scheme = "https", Port = uri.IsDefaultPort ? -1 : uri.Port }; urls.Enqueue(secure.Uri); }
            urls.Enqueue(uri);
            string last = "The song could not download.";
            bool retried = false; int attempt = 0;
            using (var total = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var client = new HttpClient(transport ?? new HttpClientHandler()))
            {
                total.CancelAfter(TimeSpan.FromMinutes(4));
                client.Timeout = Timeout.InfiniteTimeSpan;
                while (urls.Count > 0 && attempt < 3)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Uri current = urls.Dequeue(); attempt++;
                    progress.Report(attempt == 1 ? "Downloading song…" : "Retrying download connection…");
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(total.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(90));
                        try
                        {
                            using (var response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                            {
                                int code = (int)response.StatusCode;
                                if (code != 200)
                                {
                                    if (code == 429) throw new InvalidOperationException("The audio server is limiting downloads. Wait before trying again.");
                                    last = code == 401 || code == 403 || code == 404 || code == 410
                                        ? "The audio server rejected the link (HTTP " + code + "). It may have expired. Try a newly playing song."
                                        : "The audio server returned HTTP " + code + ". Please try again.";
                                    if (code >= 500 && code <= 599 && !retried) { urls.Enqueue(current); retried = true; }
                                    continue;
                                }
                                if (response.Content.Headers.ContentLength.GetValueOrDefault() > 100L * 1024 * 1024)
                                    throw new InvalidOperationException("This audio download is too large to save.");
                                string type = response.Content.Headers.ContentType == null ? "" : response.Content.Headers.ContentType.MediaType;
                                if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || type.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
                                    throw new InvalidOperationException("The audio server returned a web page instead of a song.");
                                using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                using (var output = new FileStream(input, FileMode.Create, FileAccess.Write, FileShare.None, 32768, true))
                                {
                                    byte[] buffer = new byte[32768]; int count; long size = 0;
                                    while ((count = await ReadNetworkAsync(source, buffer, timeout.Token).ConfigureAwait(false)) > 0)
                                    {
                                        size += count;
                                        if (size > 100L * 1024 * 1024) throw new InvalidOperationException("This audio download is too large to save.");
                                        await output.WriteAsync(buffer, 0, count, timeout.Token).ConfigureAwait(false);
                                    }
                                    if (size == 0) throw new InvalidOperationException("The audio download is empty.");
                                }
                                return;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            if (cancellation.IsCancellationRequested) throw;
                            last = "The audio download timed out. Check your connection and try Save MP3 again.";
                            if (total.IsCancellationRequested) break;
                            if (!retried) { urls.Enqueue(current); retried = true; }
                        }
                        catch (HttpRequestException)
                        {
                            last = "The connection to the audio server failed. Check your connection and try Save MP3 again.";
                            if (!retried) { urls.Enqueue(current); retried = true; }
                        }
                    }
                }
            }
            throw new InvalidOperationException(last);
        }
        private static async Task<int> ReadNetworkAsync(Stream source, byte[] buffer, CancellationToken cancellation)
        {
            try { return await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false); }
            catch (IOException error) { throw new HttpRequestException("The audio connection was interrupted.", error); }
        }
        internal static string DetectExtension(string path)
        {
            using (var file = File.OpenRead(path))
            {
                byte[] h = new byte[12]; int n = file.Read(h, 0, h.Length);
                if (n >= 3 && h[0] == 'I' && h[1] == 'D' && h[2] == '3') return ".mp3";
                if (n >= 2 && h[0] == 0xff && (h[1] & 0xe0) == 0xe0 && ((h[1] >> 1) & 3) == 1) return ".mp3";
                if (n >= 8 && h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p') return ".m4a";
                if (n >= 2 && h[0] == 0xff && (h[1] & 0xf6) == 0xf0) return ".aac";
                if (n >= 4 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F') return ".wav";
                return ".audio";
            }
        }
        internal static void ConvertFile(string input, string output, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (var reader = new MediaFoundationReader(input))
            {
                if (reader.TotalTime <= TimeSpan.Zero) throw new InvalidOperationException("The downloaded song contains no audio.");
                if (DetectExtension(input) == ".mp3") File.Copy(input, output);
                else MediaFoundationEncoder.EncodeToMp3(new CancellableAudio(reader, cancellation), output, 192000);
            }
            using (var verified = new MediaFoundationReader(output))
            {
                if (verified.TotalTime <= TimeSpan.Zero || DetectExtension(output) != ".mp3")
                    throw new InvalidOperationException("The MP3 file could not be verified.");
            }
        }
        private sealed class CancellableAudio : IWaveProvider
        {
            private readonly IWaveProvider source;
            private readonly CancellationToken cancellation;
            internal CancellableAudio(IWaveProvider source, CancellationToken cancellation) { this.source = source; this.cancellation = cancellation; }
            public WaveFormat WaveFormat { get { return source.WaveFormat; } }
            public int Read(byte[] buffer, int offset, int count) { cancellation.ThrowIfCancellationRequested(); return source.Read(buffer, offset, count); }
        }
    }
}

