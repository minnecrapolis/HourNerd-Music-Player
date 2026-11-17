using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using HourNerdMusicPlayer.Spotify;
class SpotifyBridgeTests
{
    static readonly string Key=new string('a',64);
    static string Post(int port,string origin,string key,string body,string method="POST",string host=null)
    {
        using(var client=new TcpClient("127.0.0.1",port)){
            client.ReceiveTimeout=5000;
            using(var stream=client.GetStream()){
                byte[] bytes=Encoding.UTF8.GetBytes(body);
                string head=method+" /exchange HTTP/1.1\r\nHost: "+(host??"127.0.0.1:"+port)+"\r\nOrigin: "+origin+"\r\nContent-Type: application/json\r\nX-HourNerd-Bridge: "+key+"\r\nContent-Length: "+bytes.Length+"\r\n\r\n";
                byte[] h=Encoding.ASCII.GetBytes(head);stream.Write(h,0,h.Length);stream.Write(bytes,0,bytes.Length);
                using(var reader=new StreamReader(stream))return reader.ReadToEnd();
            }
        }
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static int Main(){try{
        var available=new TcpListener(IPAddress.Loopback,0);available.Start();int port=((IPEndPoint)available.LocalEndpoint).Port;available.Stop();
        using(var bridge=new SpotifyBridge(Key,port)){
            Check(Post(port,"https://evil.example",Key,"{}").StartsWith("HTTP/1.1 403"),"cross-site origin rejected");
            Check(Post(port,"https://xpui.app.spotify.com","wrong","{}").StartsWith("HTTP/1.1 403"),"missing or wrong bridge key rejected");
            Check(Post(port,"https://xpui.app.spotify.com",Key,"{}","POST","attacker.example").StartsWith("HTTP/1.1 403"),"unapproved Host rejected");
            Check(Post(port,"https://xpui.app.spotify.com","","","OPTIONS").StartsWith("HTTP/1.1 204"),"Spotify preflight allowed without granting commands");
            Check(!bridge.Command("next"),"no commands queued before a live client connects");
            string response=Post(port,"https://xpui.app.spotify.com",Key,"{\"title\":\"Fixture song\",\"playing\":true}");
            Check(response.StartsWith("HTTP/1.1 200")&&bridge.Connected&&bridge.Snapshot.title=="Fixture song","authenticated Spotify snapshot accepted");
            Check(!bridge.Command("execute","powershell")&&!bridge.Command("playUri","file:///secret"),"arbitrary execution and non-Spotify URIs rejected");
            for(int i=0;i<40;i++){Check(bridge.Command("next"),"next command accepted "+i);response=Post(port,"https://xpui.app.spotify.com",Key,"{}");if(!response.Contains("\"name\":\"next\""))throw new Exception("next command was not returned");}
            response=Post(port,"https://xpui.app.spotify.com",Key,"{}");Check(!response.Contains("\"name\":\"next\""),"commands delivered once without a skip counter");
            Check(Post(port,"https://xpui.app.spotify.com",Key,"not json").StartsWith("HTTP/1.1 400"),"malformed JSON rejected");
        }
        return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
