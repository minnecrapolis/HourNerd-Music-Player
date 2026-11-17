using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace HourNerdMusicPlayer
{
    internal static class TipDeveloper
    {
        internal static string Recipient
        {
            get
            {
                try
                {
                    string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "developer-tip-email.txt");
                    var info = new System.IO.FileInfo(file);
                    if (!info.Exists || info.Length > 300) return "";
                    string email = System.IO.File.ReadAllText(file).Trim();
                    return new System.Net.Mail.MailAddress(email).Address == email ? email : "";
                }
                catch { return ""; }
            }
        }
        internal static void Show(Window owner)
        {
            if (string.IsNullOrWhiteSpace(Recipient)) { MessageBox.Show(owner, "Payment details are not included in this build.", "Support HourNerd"); return; }
            var window = new Window {
                Title = "Buy the developer a beer", Owner = owner, Width = 490,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };
            window.SetResourceReference(Window.BackgroundProperty, "HNBackground");
            window.SetResourceReference(Window.ForegroundProperty, "HNText");
            var panel = new StackPanel { Margin = new Thickness(28) };
            panel.Children.Add(new TextBlock { Text = "🍺 Buy the developer a beer", FontSize = 24, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = "Enjoying HourNerd? A tip helps me keep building. Thanks! — Chris", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 18) });
            panel.Children.Add(new TextBlock { Text = Recipient, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            var copy = new Button { Content = "Copy payment email", Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 0, 18) };
            copy.Click += (sender, args) => { try { Clipboard.SetText(Recipient); copy.Content = "Email copied"; } catch { MessageBox.Show(window, "Copy this email: " + Recipient, "Payment email"); } };
            panel.Children.Add(copy);
            panel.Children.Add(new TextBlock { Text = "Cash App", FontSize = 19, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = "Open Cash App, enter your tip, tap Pay, and paste the email above as the recipient.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 9) });
            var cash = new Button { Content = "Open Cash App", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 0, 18) };
            cash.Click += (sender, args) => OpenLink(window, "https://cash.app/"); panel.Children.Add(cash);
            panel.Children.Add(new TextBlock { Text = "Zelle", FontSize = 19, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = "Open Zelle in your banking app and send to the email above.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 9) });
            var bank = new Button { Content = "Find your bank", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 0, 18) };
            bank.Click += (sender, args) => OpenLink(window, "https://www.zelle.com/get-started"); panel.Children.Add(bank);
            panel.Children.Add(new TextBlock { Text = "Recipient: Chris Dahlkvist. Check the recipient details in your payment app before sending. Tips are optional.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 18) });
            var close = new Button { Content = "Close", Padding = new Thickness(14, 8, 14, 8) };
            close.Click += (sender, args) => window.Close(); panel.Children.Add(close);
            window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = SystemParameters.WorkArea.Height - 100 };
            window.ShowDialog();
        }
        private static void OpenLink(Window owner, string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { MessageBox.Show(owner, "Open this address in your browser:\n" + url, "Support HourNerd"); }
        }
    }
}
