using System;
using System.IO;
using System.Threading;
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
            Console.WriteLine("PASS cancellation and filename safety");return 0;
        } catch(Exception error){Console.Error.WriteLine(error);return 1;}finally{try{Directory.Delete(folder,true);}catch{}}
    }
}
