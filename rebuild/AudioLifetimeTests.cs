// Real native BASS ownership/end-callback regressions; no Pandora login or sound device.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using BassPlayer;
using Un4seen.Bass;

internal static class AudioLifetimeTests
{
    private static int passed, failed;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<SYNCPROC> Roots = new List<SYNCPROC>();
    private static object Field(object engine, string name) { return engine.GetType().GetField(name, Flags).GetValue(engine); }
    private static void Set(object engine, string name, object value) { engine.GetType().GetField(name, Flags).SetValue(engine, value); }
    private static object Call(object engine, string name, params object[] arguments) { return engine.GetType().GetMethod(name, Flags).Invoke(engine, arguments); }
    private static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Check(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static int Attach(BassAudioEngine engine, string path, Action freed)
    {
        int stream = Bass.BASS_StreamCreateFile(path, 0, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
        Assert(stream != 0, "Could not open native test stream: " + Bass.BASS_ErrorGetCode());
        ((List<int>)Field(engine, "Streams"))[0] = stream;
        var handles = (List<int>)Call(engine, "RegisterPlaybackEvents", stream, 0);
        ((List<List<int>>)Field(engine, "StreamEventSyncHandles"))[0] = handles;
        Set(engine, "_State", BassAudioEngine.PlayState.Playing);
        SYNCPROC onFree = delegate { freed(); };
        Roots.Add(onFree);
        Assert(Bass.BASS_ChannelSetSync(stream, BASSSync.BASS_SYNC_FREE, 0, onFree, IntPtr.Zero) != 0, "Could not observe native release");
        return stream;
    }
    private static void Drain(int stream)
    {
        var buffer = new float[4096];
        for (int n = 0; n < 100; n++)
        {
            if (Bass.BASS_ChannelGetData(stream, buffer, buffer.Length * 4) <= 0) return;
        }
        throw new Exception("Native decoder never reached end of test track");
    }
    private static void WriteWave(string path)
    {
        const int samples = 2205, bytes = samples * 2;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(new byte[] {82,73,70,70}); writer.Write(36 + bytes);
            writer.Write(new byte[] {87,65,86,69,102,109,116,32}); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(new byte[] {100,97,116,97}); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
    }
    public static int Main()
    {
        string path = Path.Combine(Path.GetTempPath(), "hournerd-audio-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            WriteWave(path);
            Assert(Bass.BASS_Init(0, 44100, 0, IntPtr.Zero), "BASS no-sound initialization failed: " + Bass.BASS_ErrorGetCode());
            var engine = new BassAudioEngine();
            Check("a stream is natively released only once, including concurrent releases", delegate
            {
                int freed = 0;
                int stream = Attach(engine, path, delegate { Interlocked.Increment(ref freed); });
                var threads = new List<Thread>();
                for (int n = 0; n < 8; n++)
                {
                    var thread = new Thread(delegate() { Call(engine, "FreeStream", stream); }); threads.Add(thread); thread.Start();
                }
                foreach (var thread in threads) Assert(thread.Join(5000), "Release deadlocked");
                Assert(freed == 1, "Native handle was not released exactly once");
                Assert((int)Call(engine, "GetCurrentStream") == 0, "Released handle remained owned");
            });
            Check("twenty native track ends retire the handle before advancing playback", delegate
            {
                int ended = 0, freed = 0, unretired = 0;
                using (var finished = new AutoResetEvent(false))
                {
                    engine.PlaybackStateChanged += delegate(object sender, BassAudioEngine.PlayState oldState, BassAudioEngine.PlayState newState)
                    {
                        if (newState != BassAudioEngine.PlayState.Ended) return;
                        if ((int)Call(engine, "GetCurrentStream") != 0) Interlocked.Increment(ref unretired);
                        Interlocked.Increment(ref ended); finished.Set();
                    };
                    for (int n = 0; n < 20; n++)
                    {
                        int stream = Attach(engine, path, delegate { Interlocked.Increment(ref freed); });
                        Drain(stream);
                        Assert(finished.WaitOne(5000), "Track-end callback did not complete");
                        Assert(ended == n + 1, "Duplicate track-end event");
                    }
                }
                Assert(unretired == 0, "Playback advanced before releasing the previous stream");
                Assert(freed == 20, "Native release count did not match track count");
            });
            Check("a stale queued completion cannot free a newer stream", delegate
            {
                int freed = 0, ended = 0;
                int oldStream = Attach(engine, path, delegate { Interlocked.Increment(ref freed); });
                int oldGeneration = (int)Field(engine, "_streamGeneration");
                Call(engine, "FreeStream", oldStream);
                int current = Attach(engine, path, delegate { Interlocked.Increment(ref freed); });
                engine.PlaybackStateChanged += delegate { Interlocked.Increment(ref ended); };
                // Reuse CURRENT's numeric handle with OLD's generation, simulating native handle reuse.
                Call(engine, "PlaybackEndProc", 0, current, 0, new IntPtr(oldGeneration));
                Thread.Sleep(200);
                Assert((int)Call(engine, "GetCurrentStream") == current, "Stale completion released the new stream");
                Assert(ended == 0 && freed == 1, "Stale completion advanced playback");
                Call(engine, "FreeStream", current);
            });
        }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL test setup: " + ex); }
        finally { Bass.BASS_Free(); if (File.Exists(path)) File.Delete(path); GC.KeepAlive(Roots); }
        Console.WriteLine(passed + " audio lifecycle checks passed; " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
}
