using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.Shell;
using NDesk.Options;
using Util;
using System.IO;

namespace HourNerdMusicPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {

        [STAThread]
        public static void Main()
        {
            // Launches from Explorer, shortcuts and the finish page must use the
            // same writable working folder, independent of the install location.
            try { DistributionPolicy.ConfigureWorkingDirectory(); }
            catch (Exception)
            {
                MessageBox.Show("HourNerd Music Player could not create its working folder in your Windows profile.",
                    "Unable to start", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SingleInstance<App>.InitializeAsFirstInstance("HourNerdMusicPlayerInstance"))
            {
                var application = new App();
                application.DispatcherUnhandledException += (sender, e) =>
                {
                    string report = WriteCrashReport(e.Exception);
                    MessageBox.Show("HourNerd Music Player encountered an error and must close.\r\n\r\n" +
                        e.Exception.GetType().Name + "\r\n\r\nCrash report: " + report,
                        "HourNerd Music Player crash report", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Handled = true;
                    application.Shutdown(-1);
                };
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    WriteCrashReport(e.ExceptionObject as Exception);
                application.Init();
                application.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }

        private static readonly object CrashReportLock = new object();
        private static string WriteCrashReport(Exception exception)
        {
            string directory = DistributionPolicy.ProfileDirectory;
            string path = Path.Combine(directory, "hournerdmusicplayer-crash.txt");
            try
            {
                var text = new System.Text.StringBuilder();
                text.AppendLine(DateTime.Now.ToString("O"));
                text.AppendLine("HourNerd Music Player 1.1.3 managed exception");
                for (Exception current = exception; current != null; current = current.InnerException)
                {
                    text.AppendLine(current.GetType().FullName);
                    // Omit authenticated/signed URLs from exception messages.
                    text.AppendLine(System.Text.RegularExpressions.Regex.Replace(current.Message ?? "",
                        @"https?://\S+", "[URL omitted]"));
                    text.AppendLine(current.StackTrace ?? "[no stack trace]");
                }
                lock (CrashReportLock)
                {
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(path, text.ToString() + Environment.NewLine);
                }
                return path;
            }
            catch { return "Could not write the crash report."; }
        }

        public void Init()
        {
            this.InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {       
            base.OnStartup(e);
            HandleCommandLine(System.Environment.GetCommandLineArgs());
            if(HourNerdMusicPlayer.MainWindow._clo.ShowHelp)
                Application.Current.Shutdown();
        }

        private void ShowHelp(OptionSet optionSet, string msg = null)
        {
            StringWriter sw = new StringWriter();
            optionSet.WriteOptionDescriptions(sw);
            string output = sw.ToString();
            if(msg != null)
                output += "\r\n\r\n" + msg;
            MessageBox.Show(output, "HourNerd Music Player Options");
        }

        public bool HandleCommandLine(IList<string> args)
        {
            CommandLineOptions clo = new CommandLineOptions();
            OptionSet p = new OptionSet()
               .Add("c|config=", "a {CONFIG} file to load ", delegate(string v) { clo.ConfigPath = v; })
               .Add("h|?|help", "show this message and exit", delegate(string v) { clo.ShowHelp = v != null; })
               .Add("playpause", "toggles playback", delegate(string v) { clo.TogglePlayPause = v != null; })
               .Add("next", "skips current track", delegate(string v) { clo.SkipTrack = v != null; })
               .Add("thumbsup", "rates the song as suitable for this station", delegate(string v) { clo.DoThumbsUp = v != null; })
               .Add("thumbsdown", "rates the song as unsuitable for this station", delegate(string v) { clo.DoThumbsDown = v != null; })
               .Add("s|station=", "starts station \"{STATIONNAME}\" - puts quotes around station names with spaces", delegate(string v) { clo.StationToLoad = v; })
               .Add("exit|quit", "exits HourNerd Music Player", delegate(string v) { clo.Exit = v != null; });

            try
            {
                p.Parse(args);
            }
            catch (OptionException e)
            {
                clo.ShowHelp = true;
                HourNerdMusicPlayer.MainWindow.SetCommandLine(clo);
                ShowHelp(p, e.Message);
            }

            HourNerdMusicPlayer.MainWindow.SetCommandLine(clo);

            if (clo.ShowHelp)
            {
                ShowHelp(p);
            }
            else
            {
                if (MainWindow != null)
                {
                    ((HourNerdMusicPlayer.MainWindow)MainWindow).DoCommandLine();
                }
            }

            return true;
        }

        #region ISingleInstanceApp Members
        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            //only bring app to front if called with no arguments.  If argument was passed assume user
            //is calling exe for control purposes only and does not want the app to be brought to front
            if (args.Count == 1)
            {
                ((HourNerdMusicPlayer.MainWindow)MainWindow).Show();
                ((HourNerdMusicPlayer.MainWindow)MainWindow).Activate();
                ((HourNerdMusicPlayer.MainWindow)MainWindow).WindowState = WindowState.Normal;
                ((HourNerdMusicPlayer.MainWindow)MainWindow).ShowInTaskbar = true;
            }

            return HandleCommandLine(args);
        }

        #endregion
    }
}
