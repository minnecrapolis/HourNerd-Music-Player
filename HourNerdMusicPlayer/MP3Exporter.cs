// HourNerd, GPL-3.0-or-later. NAudio is used under its MIT license.
using System;
using System.IO;
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
        internal static async Task SaveAsync(string audioUrl, string destination, IProgress<string> progress, CancellationToken cancellation)
        {
            Uri uri;
            if (!Uri.TryCreate(audioUrl, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new InvalidOperationException("This song does not have a downloadable audio stream.");
            string input = Path.Combine(Path.GetTempPath(), "HourNerd-audio-" + Guid.NewGuid().ToString("N"));
            string partial = Path.Combine(Path.GetDirectoryName(destination), ".HourNerd-" + Guid.NewGuid().ToString("N") + ".mp3");
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                using (var client = new HttpClient())
                {
                    timeout.CancelAfter(TimeSpan.FromMinutes(3));
                    progress.Report("Downloading song…");
                    using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            throw new InvalidOperationException("Pandora did not provide a downloadable song. Try while the song is still playing.");
                        if (response.Content.Headers.ContentLength.GetValueOrDefault() > 100L * 1024 * 1024)
                            throw new InvalidOperationException("This audio download is too large to save.");
                        using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new FileStream(input, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32768, true))
                        {
                            byte[] buffer = new byte[32768]; int count; long size = 0;
                            while ((count = await source.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                size += count;
                                if (size > 100L * 1024 * 1024) throw new InvalidOperationException("This audio download is too large to save.");
                                await output.WriteAsync(buffer, 0, count, timeout.Token).ConfigureAwait(false);
                            }
                            if (size == 0) throw new InvalidOperationException("The audio download is empty.");
                        }
                    }
                }
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
            catch (HttpRequestException) { throw new InvalidOperationException("The song could not download. Its stream may have expired. Try while it is still playing."); }
            finally
            {
                try { if (File.Exists(input)) File.Delete(input); } catch { }
                try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            }
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
