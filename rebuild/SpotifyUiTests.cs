using System;
using System.IO;
using System.Net;
using System.Text;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HourNerdMusicPlayer;
using HourNerdMusicPlayer.Spotify;
class SpotifyUiTests
{
    static Application app;
    static DispatcherTimer timer;
    static int stage, attempts;
    static MainWindow window;
    static ComboBox services;
    
    static object Field(string name) { return typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window); }
    static DependencyObject Find(DependencyObject parent,Predicate<DependencyObject> match) {
        if(parent==null)return null;
        if(match(parent))return parent;
        int count=VisualTreeHelper.GetChildrenCount(parent);
        for(int i=0;i<count;i++){var found=Find(VisualTreeHelper.GetChild(parent,i),match);if(found!=null)return found;}
        return null;
    }
    static bool Text(string text) { return Find((DependencyObject)window.Content,o=>o is TextBlock&&((TextBlock)o).Text==text)!=null; }
    static Button Button(string text) { return Find((DependencyObject)window.Content,o=>o is Button&&Convert.ToString(((Button)o).Content)==text) as Button; }
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message); }
    sealed class FixtureEngine : ISpotifyEngine {
        public int SignInCount;
        public SpotifySnapshot Snapshot {get;private set;}
        public FixtureEngine(){Snapshot=new SpotifySnapshot();}
        public bool Connected {get{return Snapshot.connected;}}
        public string Status {get{return Connected?"Connected to Spotify as fixture":"Sign in to Spotify to begin.";}}
        public bool Command(string name,string value=""){return Connected;}
        public void SignIn(){SignInCount++;Snapshot=new SpotifySnapshot {connected=true,account="fixture",title="Fixture song",artist="Fixture artist",entries=new[]{new SpotifyEntry {Name="Fixture playlist",uri="spotify:playlist:1234567890123456789012"}}};}
        public void SignOut(){Snapshot=new SpotifySnapshot();}
        public void Dispose(){}
    }
    static readonly FixtureEngine fixture=new FixtureEngine();
    static void Tick(object sender,EventArgs args) {
        try {
            var layout=(FrameworkElement)window.Content;
            layout.Measure(new Size(1180,760));layout.Arrange(new Rect(0,0,1180,760));layout.UpdateLayout();
            if(++attempts>90)throw new Exception("Timed out waiting for native UI startup");
            if(stage==0){
                window.UpdateLayout();
                services=(ComboBox)window.FindName("MusicService");
                Check(services!=null&&services.Items.Count==2,"native service selector contains Pandora and Spotify");
                services.SelectedIndex=1;stage++;return;
            }
            if(stage==1){
                Check(Button("Sign in to Spotify")!=null&&Text("YOUR SPOTIFY MUSIC"),"selecting Spotify displays its panel and sidebar");
                fixture.SignIn();stage++;return;
            }
            if(stage==2){
                Check(Text("Fixture song"),"authenticated song metadata appears in native UI");
                var view=(FrameworkElement)window.Content;
                view.Measure(new Size(1180,760));view.Arrange(new Rect(0,0,1180,760));view.UpdateLayout();
                var bitmap=new RenderTargetBitmap(1180,760,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"spotify-preview.png")))encoder.Save(file);
                Button("Settings").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));stage++;return;
            }
            if(stage==3){
                var settings=(FrameworkElement)Field("_settingsPage");
                Check(((ContentControl)window.FindName("cntCtrl")).Content==settings,"Settings page opens while Spotify is selected");
                Check(settings.FindName("PandoraAccountStatus")!=null&&settings.FindName("btnPandoraSignIn")!=null&&settings.FindName("btnSpotifyAccount")!=null&&settings.FindName("btnSpotifySetup")!=null,"Settings offers separate Pandora and Spotify account controls");
                services.SelectedIndex=0;stage++;return;
            }
            if(stage==4){
                Check(Text("YOUR STATIONS"),"switching to Pandora restores its station interface");
                Check(((ListBox)window.FindName("SidebarStationList")).Items.Count==0,"signed-out Pandora does not display Spotify playlists");
                services.SelectedIndex=1;stage++;return;
            }
            if(stage==5){
                var sidebar=(ListBox)window.FindName("SidebarStationList");
                Check(sidebar.Items.Count==1,"switching services preserves the Spotify library");
                Check(fixture.SignInCount==1&&fixture.Connected,"switching services preserves Spotify connection identity");
                timer.Stop();Console.Out.Flush();Environment.Exit(0);
            }
        }catch(Exception error){Console.Error.WriteLine(error);Console.Error.Flush();Environment.Exit(1);}
    }
    [STAThread] static int Main() {
        try {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            app=new Application();
            var resources=new System.Xml.XmlDocument();
            resources.Load(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","App.xaml")));
            foreach(System.Xml.XmlNode node in resources.SelectNodes("//*[local-name()='ResourceDictionary'][@Source]")) {
                string source=node.Attributes["Source"].Value;
                if(!source.StartsWith("/")&&!source.StartsWith("pack:"))source="/HourNerdMusicPlayer;component/"+source;
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri(source,UriKind.RelativeOrAbsolute) });
            }
            MainWindow.SetCommandLine(new CommandLineOptions());
            // Exercise actual WPF controls without starting native audio or requiring a desktop session.
            var pageType=typeof(MainWindow).Assembly.GetType("HourNerdMusicPlayer.Spotify.SpotifyPage");
            pageType.GetField("EngineFactory",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,new Func<ISpotifyEngine>(()=>fixture));
            window=new MainWindow();
            var settings=(Settings)Activator.CreateInstance(typeof(Settings),new object[]{null,Field("_config"),null});
            typeof(MainWindow).GetField("_settingsPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,settings);
            var view=(FrameworkElement)window.Content;
            view.Measure(new Size(1180,760));view.Arrange(new Rect(0,0,1180,760));view.UpdateLayout();
            app.DispatcherUnhandledException+=(s,e)=>{Console.Error.WriteLine(e.Exception);Console.Error.Flush();Environment.Exit(1);};
            timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(500) };timer.Tick+=Tick;timer.Start();
            Dispatcher.Run();return 1;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
