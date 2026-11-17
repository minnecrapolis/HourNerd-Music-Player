// GPL-3.0-or-later. Native Spotify playback; no Spotify app or network control port.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace HourNerdMusicPlayer.Spotify
{
    public interface ISpotifyEngine : IDisposable
    {
        bool Connected { get; }
        SpotifySnapshot Snapshot { get; }
        string Status { get; }
        bool Command(string name, string value = "");
        void SignIn();
        void SignOut();
    }
    internal sealed class SpotifyNativeEngine : ISpotifyEngine
    {
        private readonly object gate = new object();
        private Process process;
        private SpotifySnapshot snapshot = new SpotifySnapshot();
        private string status = "Sign in to Spotify to begin.";
        private bool disposed;
        private readonly string credentials = Path.Combine(Config.HourNerdMusicPlayerAppData,"SpotifyStandalone","credentials.dpapi");
        public bool Connected { get { lock(gate) return process != null && snapshot.connected; } }
        public SpotifySnapshot Snapshot { get { lock(gate) return snapshot; } }
        public string Status { get { lock(gate) return status; } }
        internal SpotifyNativeEngine()
        {
            Start();
            try {
                if(File.Exists(credentials)) {
                    byte[] protectedBytes=File.ReadAllBytes(credentials);
                    if(protectedBytes.Length>65536)throw new IOException();
                    string token=Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes,null,DataProtectionScope.CurrentUser));
                    if(token.Length>0&&token.Length<=16384)Send("restore",token);
                }
            } catch { status="Saved Spotify sign-in could not be read. Sign in again."; }
        }
        private void Start()
        {
            string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Spotify","hournerd-spotify.exe");
            if(!File.Exists(file)){status="The native Spotify engine is missing. Extract and apply the entire standalone update ZIP.";return;}
            try {
                var child=new Process { StartInfo=new ProcessStartInfo(file) {
                    UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(file),
                    RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                    StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
                },EnableRaisingEvents=true };
                child.OutputDataReceived+=Receive;
                child.ErrorDataReceived+=(s,e)=>{}; // Never log upstream tokens or signed stream URLs.
                child.Exited+=(s,e)=>{lock(gate){if(!disposed&&object.ReferenceEquals(process,s)){snapshot.connected=false;snapshot.playing=false;status="The native Spotify engine stopped. Sign in again to restart it.";}}};
                lock(gate)process=child;
                child.Start();child.BeginOutputReadLine();child.BeginErrorReadLine();
            } catch { lock(gate){process=null;status="The native Spotify engine could not start. This Windows test build needs 64-bit Windows.";} }
        }
        private void Receive(object sender,DataReceivedEventArgs args)
        {
            if(args.Data==null||args.Data.Length>262144)return;
            lock(gate)if(disposed||!object.ReferenceEquals(sender,process))return;
            try {
                var json=new JavaScriptSerializer { MaxJsonLength=262144 };
                var message=json.Deserialize<Dictionary<string,object>>(args.Data);
                object type;if(!message.TryGetValue("type",out type))return;
                if((string)type=="state") {
                    var update=json.ConvertToType<SpotifySnapshot>(message["snapshot"]);
                    lock(gate){snapshot=update;status=string.IsNullOrEmpty(update.error)?"Connected to Spotify"+(string.IsNullOrEmpty(update.account)?"":" as "+update.account):update.error;}
                } else if((string)type=="pairing") {
                    string code=Convert.ToString(message["code"]),link=Convert.ToString(message["url"]);Uri uri;
                    lock(gate)status="Approve Spotify sign-in in your browser. Pairing code: "+code;
                    if(Uri.TryCreate(link,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&
                        (uri.Host=="accounts.spotify.com"||uri.Host=="spotify.com"||uri.Host=="www.spotify.com"))
                        try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch{}
                } else if((string)type=="credentials") {
                    string token=Convert.ToString(message["refreshToken"]);if(token.Length==0||token.Length>16384)return;
                    byte[] protectedBytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(token),null,DataProtectionScope.CurrentUser);
                    lock(gate){if(disposed||!object.ReferenceEquals(sender,process))return;Directory.CreateDirectory(Path.GetDirectoryName(credentials));string temporary=credentials+".new";File.WriteAllBytes(temporary,protectedBytes);if(File.Exists(credentials))File.Replace(temporary,credentials,null);else File.Move(temporary,credentials);}
                }
            }catch { lock(gate)status="Spotify returned an unreadable response. Try signing in again."; }
        }
        private bool Send(string name,string value="")
        {
            try { lock(gate){if(disposed||process==null||process.HasExited)return false;process.StandardInput.WriteLine(new JavaScriptSerializer().Serialize(new {name,value}));process.StandardInput.Flush();return true;} }
            catch { lock(gate)status="The native Spotify connection stopped. Sign in again.";return false; }
        }
        public bool Command(string name,string value="")
        {
            if(!new HashSet<string>{"play","pause","toggle","next","previous","library","search","playUri"}.Contains(name)||value==null||value.Length>200)return false;
            if(name=="playUri"&&!SpotifyBridge.ValidUri(value))return false;
            return Connected&&Send(name,value);
        }
        private void Stop()
        {
            Process child;lock(gate){child=process;process=null;snapshot.connected=false;snapshot.playing=false;}
            if(child==null)return;
            try{child.StandardInput.Close();if(!child.WaitForExit(700))child.Kill();}catch{}finally{child.Dispose();}
        }
        public void SignIn(){Stop();try{if(File.Exists(credentials))File.Delete(credentials);}catch{}Start();if(Send("connect")){lock(gate)status="Starting Spotify sign-in…";}}
        public void SignOut(){Stop();try{if(File.Exists(credentials))File.Delete(credentials);}catch{}lock(gate){snapshot=new SpotifySnapshot();status="Signed out of Spotify.";}}
        public void Dispose(){disposed=true;Stop();}
    }
}
