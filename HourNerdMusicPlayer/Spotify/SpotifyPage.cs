using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HourNerdMusicPlayer.Spotify
{
    internal sealed class SpotifyPage : UserControl, IDisposable
    {
        private SpotifyBridge bridge;
        private readonly DispatcherTimer timer;
        private TextBlock status, title, artist, album, duration, adblock;
        private Image artwork;
        private ListBox results;
        private TextBox search;
        private Button play, next, previous, heart, library, find;
        private string artworkUrl="", entriesSignature="";
        private bool active;
        internal event Action<SpotifyEntry[]> EntriesChanged;
        internal void SetupSpotify() { OpenFile("Setup-Spotify.cmd"); }
        internal void OpenSpotify() { OpenUri("spotify:"); }
        internal bool Connected { get { return bridge!=null && bridge.Connected; } }
        internal SpotifyPage()
        {
            var scroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
            var root=new StackPanel { Margin=new Thickness(4) };scroll.Content=root;Content=scroll;
            root.Children.Add(Label("Spotify",30,true));
            root.Children.Add(Label("Your music, with a little more room to breathe.",14,false));
            status=Label("Connecting to Spotify…",14,false);status.Margin=new Thickness(0,16,0,8);root.Children.Add(status);
            var setupBar=new WrapPanel();root.Children.Add(setupBar);
            setupBar.Children.Add(ActionButton("Set up Spotify",()=>OpenFile("Setup-Spotify.cmd")));
            setupBar.Children.Add(ActionButton("Open Spotify",()=>OpenUri("spotify:")));
            setupBar.Children.Add(ActionButton("Setup guide",()=>OpenFile("SPOTIFY-README.txt")));
            var now=new Grid { Margin=new Thickness(0,20,0,14) };now.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(180) });now.ColumnDefinitions.Add(new ColumnDefinition());root.Children.Add(now);
            artwork=new Image { Width=160,Height=160,Stretch=Stretch.UniformToFill,HorizontalAlignment=HorizontalAlignment.Left };now.Children.Add(artwork);
            var details=new StackPanel { VerticalAlignment=VerticalAlignment.Center };Grid.SetColumn(details,1);now.Children.Add(details);
            title=Label("Open Spotify and play a song",25,true);artist=Label("",17,false);album=Label("",13,false);duration=Label("",12,false);details.Children.Add(title);details.Children.Add(artist);details.Children.Add(album);details.Children.Add(duration);
            var controls=new WrapPanel { Margin=new Thickness(0,12,0,0) };details.Children.Add(controls);
            previous=ActionButton("Previous",()=>Send("previous"));play=ActionButton("Play",()=>Send("toggle"));next=ActionButton("Skip →",()=>Send("next"));heart=ActionButton("Like",()=>Send("heart"));
            controls.Children.Add(previous);controls.Children.Add(play);controls.Children.Add(next);controls.Children.Add(heart);
            adblock=Label("Ad blocking status unavailable until Spotify connects.",12,false);root.Children.Add(adblock);
            root.Children.Add(Label("Find a song or playlist",16,true));
            var browse=new DockPanel { Margin=new Thickness(0,20,0,12) };root.Children.Add(browse);
            library=ActionButton("My playlists",()=>Send("library"));DockPanel.SetDock(library,Dock.Left);browse.Children.Add(library);
            find=ActionButton("Search",()=>Send("search",search.Text));DockPanel.SetDock(find,Dock.Right);browse.Children.Add(find);
            search=new TextBox { Padding=new Thickness(9,7,9,7),Margin=new Thickness(8,0,8,7),MinWidth=160 };search.SetResourceReference(Control.BackgroundProperty,"HNInput");search.SetResourceReference(Control.ForegroundProperty,"HNText");search.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter)Send("search",search.Text);};browse.Children.Add(search);
            results=new ListBox { MinHeight=110,MaxHeight=260,Padding=new Thickness(8),BorderThickness=new Thickness(0) };results.SetResourceReference(Control.BackgroundProperty,"HNSurface");results.SetResourceReference(Control.ForegroundProperty,"HNText");results.MouseDoubleClick+=(s,e)=>PlaySelected();results.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter)PlaySelected();};root.Children.Add(results);
            root.Children.Add(ActionButton("Play selected",PlaySelected));
            root.Children.Add(Label("Spotify stays open in the background for audio. Playlists and songs play through your signed-in Spotify session.",12,false));
            try{bridge=new SpotifyBridge(SpotifyBridge.ReadOrCreateKey());}catch{status.Text="The local Spotify connection could not start. Close any other HourNerd instance, then reopen the player.";}
            timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(750) };timer.Tick+=(s,e)=>Refresh();timer.Start();Refresh();
        }
        private TextBlock Label(string value,double size,bool bold)
        {var t=new TextBlock { Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal };t.SetResourceReference(TextBlock.ForegroundProperty,bold?"HNText":"HNSecondaryText");return t;}
        private Button ActionButton(string caption,Action action)
        {var b=new Button { Content=caption,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,7) };b.SetResourceReference(Control.BackgroundProperty,"HNSurface");b.SetResourceReference(Control.ForegroundProperty,"HNText");b.Click+=(s,e)=>action();return b;}
        private void OpenFile(string name)
        {string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Spotify",name);try{if(!File.Exists(file))throw new IOException();Process.Start(new ProcessStartInfo(file){UseShellExecute=true});}catch{status.Text="The Spotify setup files were not found. Extract the entire update and run Apply-MP3-Update.cmd.";}}
        private void OpenUri(string uri){try{Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});}catch{status.Text="Install the Spotify desktop app first, then use Set up Spotify.";}}
        internal void Send(string command,string value="")
        {if(bridge==null||!bridge.Command(command,value))status.Text="Spotify is not connected yet. Open it and run Set up Spotify once.";}
        internal void SetActive(bool value)
        {active=value;if(!value&&bridge!=null)bridge.Command("pause");if(value&&bridge!=null&&bridge.Connected)bridge.Command("library");Refresh();}
        private void PlaySelected(){var selected=results.SelectedItem as SpotifyEntry;if(selected!=null)Send("playUri",selected.uri);}
        private void Refresh()
        {
            bool connected=Connected;
            foreach(var button in new[]{play,next,previous,heart,library,find})button.IsEnabled=connected&&active;
            if(bridge==null)return;
            if(!connected){status.Text="Not connected. Open the Spotify desktop app, sign in there, and run Set up Spotify once.";adblock.Text="Ad blocking status unavailable until Spotify connects.";return;}
            var snapshot=bridge.Snapshot;
            status.Text=string.IsNullOrEmpty(snapshot.error)?"Connected to Spotify Desktop":snapshot.error;
            title.Text=string.IsNullOrEmpty(snapshot.title)?"Choose a playlist or search for a song":snapshot.title;artist.Text=snapshot.artist??"";album.Text=snapshot.album??"";
            play.Content=snapshot.playing?"Pause":"Play";
            duration.Text=Clock(snapshot.position)+" / "+Clock(snapshot.duration);
            adblock.Text=snapshot.adblockLoaded?"Ad blocking extension loaded · HourNerd adds no skip counter":"Ad blocking extension not detected. Run Set up Spotify, then restart Spotify.";
            var rows=(snapshot.entries??new SpotifyEntry[0]).Where(e=>e!=null&&!string.IsNullOrEmpty(e.name)&&SpotifyBridge.ValidUri(e.uri)).Take(100).ToArray();
            string signature=string.Join("|",rows.Select(e=>e.uri+e.name));
            if(signature!=entriesSignature){var selected=results.SelectedItem as SpotifyEntry;results.ItemsSource=rows;if(selected!=null)results.SelectedItem=rows.FirstOrDefault(e=>e.uri==selected.uri);entriesSignature=signature;if(EntriesChanged!=null)EntriesChanged(rows); }
            string image=snapshot.artwork??"";
            Uri url;
            if(image!=artworkUrl){artworkUrl=image;artwork.Source=null;if(Uri.TryCreate(image,UriKind.Absolute,out url)&&url.Scheme=="https"&&url.Host=="i.scdn.co")try{artwork.Source=new BitmapImage(url);}catch{}}
        }
        private static string Clock(double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)seconds=0;var t=TimeSpan.FromSeconds(Math.Min(seconds,86400));return ((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
        public void Dispose(){timer.Stop();if(bridge!=null){bridge.Command("pause");bridge.Dispose();}}
    }
}
