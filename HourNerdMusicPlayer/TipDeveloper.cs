using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace HourNerdMusicPlayer
{
    internal static class TipDeveloper
    {
        internal const string Recipient = "";
        // Populate only with the recipient's verified bank-generated Zelle share URL.
        private const string ZelleShareUrl = "";

        internal static void Show(Window owner)
        {
            if (string.IsNullOrWhiteSpace(Recipient) && string.IsNullOrWhiteSpace(ZelleShareUrl))
            {
                MessageBox.Show(owner, "A public tipping link has not been configured for this build.", "Tip the Developer");
                return;
            }
            var window = new Window
            {
                Title = "Tip the Developer", Owner = owner, Width = 460,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };
            window.SetResourceReference(Window.BackgroundProperty, "HNBackground");
            window.SetResourceReference(Window.ForegroundProperty, "HNText");
            var panel = new StackPanel { Margin = new Thickness(28) };
            panel.Children.Add(new TextBlock { Text = "Tip the Developer", FontSize = 25, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = "Enjoying the player? Tips support continued development. Thank you!",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 18) });
            panel.Children.Add(new TextBlock { Text = "Chris Dahlkvist · Zelle", FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = Recipient, FontSize = 19, Margin = new Thickness(0, 8, 0, 18) });
            panel.Children.Add(new TextBlock { Text = "Open Zelle in your banking app and send to the address above. Confirm the recipient name before sending.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
            var copy = new Button { Content = "Copy Zelle address", Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 0, 9) };
            copy.Click += (sender, args) =>
            {
                try { Clipboard.SetText(Recipient); copy.Content = "Address copied"; }
                catch { MessageBox.Show(window, "Copy this address: " + Recipient, "Zelle recipient"); }
            };
            panel.Children.Add(copy);
            if (!string.IsNullOrWhiteSpace(ZelleShareUrl))
            {
                var pay = new Button { Content = "Open Zelle payment link", Margin = new Thickness(0, 0, 0, 9) };
                pay.Click += (sender, args) => OpenLink(window, ZelleShareUrl);
                panel.Children.Add(pay);
            }
            var bank = new Button { Content = "Find my bank on Zelle", Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 0, 9) };
            bank.Click += (sender, args) => OpenLink(window, "https://www.zelle.com/get-started");
            panel.Children.Add(bank);
            var close = new Button { Content = "Close", Padding = new Thickness(14, 8, 14, 8) };
            close.Click += (sender, args) => window.Close();
            panel.Children.Add(close);
            window.Content = panel;
            window.ShowDialog();
        }

        private static void OpenLink(Window owner, string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { MessageBox.Show(owner, "Open this address in your browser:\n" + url, "Zelle"); }
        }
    }
}
