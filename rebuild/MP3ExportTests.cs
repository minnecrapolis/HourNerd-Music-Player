using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;
using HourNerdMusicPlayer;
class MP3ExportTests
{
    static int Main()
    {
        string folder=Path.Combine(Path.GetTempPath(),"HourNerd-MP3Test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            string wav=Path.Combine(folder,"source.wav"),aac=Path.Combine(folder,"source.m4a"),mp3=Path.Combine(folder,"output.mp3");
            using(var writer=new WaveFileWriter(wav,new WaveFormat(44100,16,2))) {
                for(int i=0;i<44100;i++) {short sample=(short)(Math.Sin(i*2*Math.PI*440/44100)*6000);writer.WriteByte((byte)sample);writer.WriteByte((byte)(sample>>8));writer.WriteByte((byte)sample);writer.WriteByte((byte)(sample>>8));}
            }
            using(var reader=new WaveFileReader(wav)) MediaFoundationEncoder.EncodeToAac(reader,aac,128000);
            MP3Exporter.ConvertFile(aac,mp3,CancellationToken.None);
            if(MP3Exporter.DetectExtension(mp3)!=".mp3")throw new Exception("Expected a real MP3 header");
            using(var reader=new MediaFoundationReader(mp3)) {if(reader.TotalTime.TotalSeconds<0.9||reader.TotalTime.TotalSeconds>1.4)throw new Exception("MP3 duration mismatch");byte[] data=new byte[88200];int count=reader.Read(data,0,data.Length);if(count<1000||Array.TrueForAll(data,b=>b==0))throw new Exception("MP3 is silent or truncated");}
            Console.WriteLine("PASS AAC converted to real MP3, with valid duration and decoded audio");
            string copy=Path.Combine(folder,"copy.mp3");MP3Exporter.ConvertFile(mp3,copy,CancellationToken.None);
            if(Convert.ToBase64String(File.ReadAllBytes(copy))!=Convert.ToBase64String(File.ReadAllBytes(mp3)))throw new Exception("MP3 should be copied losslessly");
            Console.WriteLine("PASS existing MP3 preserved byte for byte");
            var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{MP3Exporter.ConvertFile(wav,Path.Combine(folder,"cancel.mp3"),cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
            if(!cancelled)throw new Exception("Cancellation ignored");
            if(MP3Exporter.SafeName("../invalid: name/?").Contains("/"))throw new Exception("Unsafe filename");
            Console.WriteLine("PASS cancellation and filename safety");
            var handler=new RetryFixture(File.ReadAllBytes(aac),false);
            string downloaded=Path.Combine(folder,"downloaded.mp3");
            MP3Exporter.SaveAsync("https://audio.example/file?token=a%2Bb%26c",downloaded,new Progress<string>(_=>{}),CancellationToken.None,handler).GetAwaiter().GetResult();
            if(handler.Calls!=2 || MP3Exporter.DetectExtension(downloaded)!=".mp3")throw new Exception("Transient failure did not retry into a valid MP3");
            Console.WriteLine("PASS interrupted download retries and produces a verified MP3");
            var limited=new RetryFixture(File.ReadAllBytes(aac),true);bool rateLimited=false;
            try{MP3Exporter.SaveAsync("https://audio.example/file",Path.Combine(folder,"limited.mp3"),new Progress<string>(_=>{}),CancellationToken.None,limited).GetAwaiter().GetResult();}catch(InvalidOperationException e){rateLimited=e.Message.Contains("limiting");}
            if(!rateLimited || limited.Calls!=1 || File.Exists(Path.Combine(folder,"limited.mp3")))throw new Exception("Rate limit should stop without a file");
            Console.WriteLine("PASS rate limits stop without retry or a partial destination");return 0;
        } catch(Exception error){Console.Error.WriteLine(error);return 1;}finally{try{Directory.Delete(folder,true);}catch{}}
    }
}


class RetryFixture:HttpMessageHandler
{
    public int Calls;
    private readonly byte[] audio;
    private readonly bool limited;
    public RetryFixture(byte[] audio,bool limited){this.audio=audio;this.limited=limited;}
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
    {
        Calls++;

        var response=new HttpResponseMessage(limited?(HttpStatusCode)429:HttpStatusCode.OK);
        response.Content=!limited&&Calls==1?(HttpContent)new StreamContent(new InterruptedStream(audio)):new ByteArrayContent(audio);
        response.Content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mp4");
        return Task.FromResult(response);
    }
}

class InterruptedStream:MemoryStream
{
    private bool first=true;
    public InterruptedStream(byte[] bytes):base(bytes){}
    public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken cancellation)
    {
        if(!first)throw new IOException("fixture interruption after partial audio");
        first=false;return base.ReadAsync(buffer,offset,Math.Min(128,count),cancellation);
    }
}
