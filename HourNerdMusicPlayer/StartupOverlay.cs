using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HourNerdMusicPlayer
{
    // Application-owned startup branding. No third-party binaries or windows are modified.
    internal sealed class StartupOverlay : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private readonly uint _processId = (uint)Process.GetCurrentProcess().Id;
        private bool _complete;

        internal StartupOverlay(Window owner)
        {
            Owner = owner;
            Title = "HourNerd Music Player";
            Width = Math.Min(720, SystemParameters.WorkArea.Width - 32);
            Height = Math.Min(460, SystemParameters.WorkArea.Height - 32);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Background = Brush("#13171D");
            Foreground = Brush("#F1F4F8");
            FontFamily = new FontFamily("Segoe UI");

            var content = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(28)
            };
            content.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/HourNerdMusicPlayer;component/Images/main_icon.png")),
                Width = 132, Height = 132, Margin = new Thickness(0, 0, 0, 20)
            });
            content.Children.Add(new TextBlock
            {
                Text = "HourNerd", FontSize = 42, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            content.Children.Add(new TextBlock
            {
                Text = "MUSIC PLAYER", FontSize = 14, Foreground = Brush("#F18B36"),
                Margin = new Thickness(0, 4, 0, 22), HorizontalAlignment = HorizontalAlignment.Center
            });
            content.Children.Add(new ProgressBar
            {
                Width = 230, Height = 4, IsIndeterminate = true,
                Foreground = Brush("#F18B36"), Background = Brush("#354152")
            });
            content.Children.Add(new TextBlock
            {
                Text = "Starting your music...", FontSize = 13,
                Foreground = Brush("#B4C0D0"), Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Content = new Border
            {
                BorderBrush = Brush("#F18B36"), BorderThickness = new Thickness(1), Child = content
            };

            _timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += Tick;
            Closed += delegate { _timer.Stop(); _timer.Tick -= Tick; };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) { e.Handled = true; Close(); }
            };
            Loaded += delegate { _timer.Start(); };
        }

        internal void Complete()
        {
            _complete = true;
        }

        private void Tick(object sender, EventArgs e)
        {
            // Keep branding through the audio library's normal startup interval.
            // Never wait for a network request, sign-in or playback under this window.
            if ((_complete && _elapsed.Elapsed.TotalSeconds >= 6) || _elapsed.Elapsed.TotalSeconds >= 15)
            {
                Close();
                return;
            }
            uint foregroundProcess;
            GetWindowThreadProcessId(GetForegroundWindow(), out foregroundProcess);
            bool inThisApp = foregroundProcess == _processId;
            Topmost = inThisApp;
            if (inThisApp)
            {
                // Keep our own startup window above other startup windows in this process,
                // without activating it or covering another foreground application.
                SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), 0, 0, 0, 0,
                    0x0001 | 0x0002 | 0x0010); // NOSIZE | NOMOVE | NOACTIVATE
            }
        }

        private static Brush Brush(string color)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color);
            brush.Freeze();
            return brush;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
