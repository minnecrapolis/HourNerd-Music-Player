/*
 * Copyright 2012 - Adam Haile
 * Modifications Copyright 2026 - Chris Dahlkvist (HourNerd Music Player)
 * http://adamhaile.net
 *
 * This file is part of Elpis.
 * Elpis is free software: you can redistribute it and/or modify 
 * it under the terms of the GNU General Public License as published by 
 * the Free Software Foundation, either version 3 of the License, or 
 * (at your option) any later version.
 * 
 * Elpis is distributed in the hope that it will be useful, 
 * but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License 
 * along with Elpis. If not, see http://www.gnu.org/licenses/.
*/

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PandoraSharpPlayer;
using Util;

namespace HourNerdMusicPlayer
{
    /// <summary>
    /// Interaction logic for LoginPage.xaml
    /// </summary>
    public partial class LoginPage : UserControl
    {
        #region Delegates

        public delegate void ConnectingEventHandler();

        #endregion

        private readonly Config _config;
        private readonly Player _player;
        private ErrorCodes _error = ErrorCodes.SUCCESS;
        private bool _loginFailed = false;

        private const string _initEmail = "enter email address";
        private const string _initPass = "enter password";

        public ErrorCodes ErrorCode { get { return _error; } set { _error = value; } }

        public bool LoginFailed { get { return _loginFailed; } set { _loginFailed = value; } }
        public LoginPage(Player player, Config config)
        {
            _config = config;

            _player = player;
            _player.ConnectionEvent += _player_ConnectionEvent;

            InitializeComponent();
        }

        public event ConnectingEventHandler ConnectingEvent;

        private void ShowError()
        {
            this.BeginDispatch(() =>
                                   {
                                       lblError.Text = Errors.GetErrorMessage(_error);
                                       //WaitScreen.Visibility = Visibility.Hidden;
                                   });
        }

        private void _player_ConnectionEvent(object sender, bool state, ErrorCodes code)
        {
            if (!state)
            {
                _loginFailed = true;
                Log.O("Connection Error: {0} - {1}", code.ToString(), Errors.GetErrorMessage(code));

                _error = code;
                ShowError();
            }
            else
            {
                this.BeginDispatch(() =>
                                       {
                                           _config.Fields.Login_Email = _player.Email;
                                           _config.Fields.Login_Password = _player.Password;

                                           //In case AudioFormat was changed because user does not have subscription
                                           _config.Fields.Pandora_AudioFormat = _player.AudioFormat;

                                           _config.SaveConfig();
                                       });
            }
        }

        public void Login()
        {
            if (!PandoraSharp.Pandora.HasConnectionConfiguration)
            {
                lblError.Text = "Choose Pandora connection setup before signing in.";
                return;
            }
            _loginFailed = false;
            //WaitScreen.Visibility = Visibility.Visible;
            _player.Connect(txtEmail.Text, txtPassword.Password);
            if (ConnectingEvent != null)
                ConnectingEvent();
        }

        private void ConnectionSetup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window { Title = "Pandora connection setup", Width = 440,
                SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this) };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "Enter the partner connection password and both protocol keys supplied separately by your provider or administrator. These are separate from your personal Pandora account password. They are encrypted for your Windows account and stay on this computer.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            panel.Children.Add(new TextBlock { Text = "Partner connection password" });
            var input = new PasswordBox { Height = 30, MaxLength = 512 };
            panel.Children.Add(input);
            panel.Children.Add(new TextBlock { Text = "Incoming (decryption) key", Margin = new Thickness(0, 8, 0, 0) });
            var incoming = new PasswordBox { Height = 30, MaxLength = 56 };
            panel.Children.Add(incoming);
            panel.Children.Add(new TextBlock { Text = "Outgoing (encryption) key", Margin = new Thickness(0, 8, 0, 0) });
            var outgoing = new PasswordBox { Height = 30, MaxLength = 56 };
            panel.Children.Add(outgoing);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            panel.Children.Add(status);
            var save = new Button { Content = "Save connection", IsDefault = true, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(8) };
            save.Click += delegate
            {
                try
                {
                    PandoraSharp.Pandora.SaveConnection(input.Password, incoming.Password, outgoing.Password);
                    input.Clear(); incoming.Clear(); outgoing.Clear();
                    lblError.Text = "Connection value saved. Sign in with your own Pandora account.";
                    dialog.DialogResult = true;
                }
                catch (System.Exception)
                {
                    status.Text = "Could not save the connection value. Enter all three values (keys: 4 to 56 UTF-8 bytes), and check that your Windows profile is writable.";
                }
            };
            panel.Children.Add(save);
            dialog.Content = panel;
            dialog.Loaded += delegate { input.Focus(); };
            dialog.ShowDialog();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            Login();
        }

        private void txtEmail_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HidePasswordMask();
                txtPassword.Focus();
                txtPassword.SelectAll();
            }
        }

        private void txtPassword_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Login();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            txtEmail.SetResourceReference(Control.ForegroundProperty, "HNText");
            txtEmail.Text = _config.Fields.Login_Email;
            if (txtEmail.Text == string.Empty)
            {
                txtEmail.SetResourceReference(Control.ForegroundProperty, "HNMutedText");
                txtEmail.Text = _initEmail;
            }

            txtPassword.Password = _config.Fields.Login_Password;
            if (txtPassword.Password == string.Empty)
            {
                txtPasswordMask.Text = _initPass;
                txtPasswordMask.Visibility = Visibility.Visible;
            }
            else
            {
                txtPasswordMask.Visibility = Visibility.Hidden;
            }

            lblError.Text = string.Empty;


            if (!_loginFailed && _config.Fields.Login_AutoLogin &&
                (!string.IsNullOrEmpty(_config.Fields.Login_Email)) &&
                (!string.IsNullOrEmpty(_config.Fields.Login_Password)))
            {
                Login();
            }

            if (_loginFailed)
                ShowError();

            txtEmail.Focus();
            txtEmail.SelectAll();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            this.BeginDispatch(() => WaitScreen.Visibility = Visibility.Hidden);
        }

        private void ClearEmail()
        {
            if (txtEmail.Text == _initEmail)
            {
                txtEmail.SetResourceReference(Control.ForegroundProperty, "HNText");
                txtEmail.Text = string.Empty;
            }
        }

        private void txtEmail_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ClearEmail();
        }

        private void txtEmail_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ClearEmail();
        }

        private void HidePasswordMask()
        {
            if (txtPasswordMask.Visibility == Visibility.Visible) txtPasswordMask.Visibility = Visibility.Hidden;
            txtPassword.Focus();
        }
        private void txtPasswordMask_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HidePasswordMask();
        }

        private void txtPassword_GotFocus(object sender, RoutedEventArgs e)
        {
            HidePasswordMask();
        }

        private void Register_Click(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }
    }
}
