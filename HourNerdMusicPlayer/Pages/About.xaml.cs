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

namespace HourNerdMusicPlayer
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : UserControl
    {
        #region Delegates

        public delegate void CloseEvent();

        #endregion

        public About()
        {
            InitializeComponent();
        }

        public string Version { get; set; }
        public event CloseEvent Close;

        private void TipDeveloper_Click(object sender, RoutedEventArgs e)
        {
            TipDeveloper.Show(Window.GetWindow(this));
        }

        private void RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }
    }
}