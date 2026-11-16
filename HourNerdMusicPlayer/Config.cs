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

using System;
using System.Collections.Generic;
using System.IO;
using HourNerdMusicPlayer.Hotkeys;
using PandoraSharp;
using Util;
using System.Windows;
using System.Windows.Input;
using MaterialDesignColors;

namespace HourNerdMusicPlayer
{
    public class HotkeyConfig: HotKey
    {
        private HotkeyConfig(RoutedUICommand c, Key k, ModifierKeys m) : base(c, k, m) { }

        public HotkeyConfig(HotKey h)
        {
            Command = h.Command;
            Key = h.Key;
            Modifiers = h.Modifiers;
            Global = h.Global;
            Enabled = h.Enabled;
        }

        public HotkeyConfig(string data, HotkeyConfig def)
        {
            var split = data.Split('*');

            bool success = false;
            if (split.Length == 5)
            {
                try
                {
                    Command = PlayerCommands.getCommandByName(split[0]);
                    Key = (Key)Enum.Parse(typeof(Key), split[1]);
                    Modifiers = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), split[2]);
                    Global = bool.Parse(split[3]);
                    Enabled = bool.Parse(split[4]);
                    success = true;
                }
                catch{}
            }

            if(!success)
            {
                Key = def.Key;
                Modifiers = def.Modifiers;
                Enabled = def.Enabled;
            }
        }

        public static HotkeyConfig Default
        {
            get { return new HotkeyConfig(PlayerCommands.PlayPause,Key.Space,ModifierKeys.None); }
        }

        public override string ToString()
        {
            return Command.Name + "*" + Key.ToString() + "*" + Modifiers.ToString() + "*" + Global.ToString() + "*" + Enabled.ToString();
        }
    }

    public struct ConfigItems
    {
        public static MapConfigEntry Debug_WriteLog = new MapConfigEntry("Debug_WriteLog", false);
        public static MapConfigEntry Debug_Logpath = new MapConfigEntry("Debug_Logpath", Config.HourNerdMusicPlayerAppData);
        public static MapConfigEntry Debug_Timestamp = new MapConfigEntry("Debug_Timestamp", false);

        public static MapConfigEntry Login_Email = new MapConfigEntry("Login_Email", "");
        public static MapConfigEntry Login_Password = new MapConfigEntry("Login_Password", "");
        public static MapConfigEntry Login_AutoLogin = new MapConfigEntry("Login_AutoLogin", true);

        public static MapConfigEntry Pandora_AudioFormat = new MapConfigEntry("Pandora_AudioFormat",
                                                                              PAudioFormat.AACPlus);

        public static MapConfigEntry Pandora_AutoPlay = new MapConfigEntry("Pandora_AutoPlay", false);
        public static MapConfigEntry Pandora_LastStationID = new MapConfigEntry("Pandora_LastStationID", "");

        public static MapConfigEntry Pandora_StationSortOrder = new MapConfigEntry("Pandora_StationSortOrder",
                                                                                   Pandora.SortOrder.DateDesc.ToString());
        public static MapConfigEntry Logging_LogOnSongLike = new MapConfigEntry("Logging_LogOnSongLike", false);
        public static MapConfigEntry Logging_LogOnSongLikeFile = new MapConfigEntry("Logging_LogOnSongLikeFile", Path.Combine(Config.HourNerdMusicPlayerAppData, @"\LikedSongs.txt"));

        public static MapConfigEntry Proxy_Address = new MapConfigEntry("Proxy_Address", "");
        public static MapConfigEntry Proxy_Port = new MapConfigEntry("Proxy_Port", 0);
        public static MapConfigEntry Proxy_User = new MapConfigEntry("Proxy_User", "");
        public static MapConfigEntry Proxy_Password = new MapConfigEntry("Proxy_Password", "");

        public static MapConfigEntry HourNerdMusicPlayer_Version = new MapConfigEntry("HourNerdMusicPlayer_Version", (new Version()).ToString());
        public static MapConfigEntry HourNerdMusicPlayer_InstallID = new MapConfigEntry("HourNerdMusicPlayer_InstallID", Guid.NewGuid().ToString());
        public static MapConfigEntry HourNerdMusicPlayer_CheckUpdates = new MapConfigEntry("HourNerdMusicPlayer_CheckUpdates", false);
        public static MapConfigEntry HourNerdMusicPlayer_CheckBetaUpdates = new MapConfigEntry("HourNerdMusicPlayer_CheckBetaUpdates", false);
        public static MapConfigEntry HourNerdMusicPlayer_RemoteControlEnabled = new MapConfigEntry("HourNerdMusicPlayer_RemoteControlEnabled", !DistributionPolicy.IsPublicDistribution);
        public static MapConfigEntry HourNerdMusicPlayer_MinimizeToTray = new MapConfigEntry("HourNerdMusicPlayer_MinimizeToTray", false);
        public static MapConfigEntry HourNerdMusicPlayer_ShowTrayNotifications = new MapConfigEntry("HourNerdMusicPlayer_ShowTrayNotifications", true);
        public static MapConfigEntry HourNerdMusicPlayer_StartupLocation = new MapConfigEntry("HourNerdMusicPlayer_StartupLocation", "");
        public static MapConfigEntry HourNerdMusicPlayer_StartupSize = new MapConfigEntry("HourNerdMusicPlayer_StartupSize", "");
        public static MapConfigEntry HourNerdMusicPlayer_Volume = new MapConfigEntry("HourNerdMusicPlayer_Volume", 100);
        public static MapConfigEntry HourNerdMusicPlayer_PauseOnLock = new MapConfigEntry("HourNerdMusicPlayer_PauseOnLock", false);
        public static MapConfigEntry HourNerdMusicPlayer_MaxHistory = new MapConfigEntry("HourNerdMusicPlayer_MaxHistory", 8);

        public static MapConfigEntry LastFM_Scrobble = new MapConfigEntry("LastFM_Scrobble", false);
        public static MapConfigEntry LastFM_SessionKey = new MapConfigEntry("LastFM_SessionKey", "");

        public static MapConfigEntry HotKeysList = new MapConfigEntry("HotKeysList",new Dictionary<int,string>());

        //public static MapConfigEntry Misc_ForceSSL = new MapConfigEntry("Misc_ForceSSL", false);
        public static MapConfigEntry System_OutputDevice = new MapConfigEntry("System_OutputDevice", "");
        public static MapConfigEntry Appearance_Theme = new MapConfigEntry("Appearance_Theme", "Dark");
        public static MapConfigEntry Current_Color = new MapConfigEntry("Current_Color", "");
    }

    public struct ConfigDropDownItem
    {
        public string Display { get; set; }
        public string Value { get; set; }
    }

    public struct ConfigFields
    {
        public bool Debug_WriteLog { get; set; }
        public string Debug_Logpath { get; set; }
        public bool Debug_Timestamp { get; set; }

        public string Login_Email { get; set; }
        public string Login_Password { get; set; }
        public bool Login_AutoLogin { get; set; }

        public string Pandora_AudioFormat { get; set; }
        public bool Pandora_AutoPlay { get; set; }
        public string Pandora_LastStationID { get; set; }
        public string Pandora_StationSortOrder { get; set; }

        public bool Logging_LogOnSongLike { get; set; }
        public string Logging_LogOnSongLikeFile { get; set; }

        public string Proxy_Address { get; set; }
        public int Proxy_Port { get; set; }
        public string Proxy_User { get; set; }
        public string Proxy_Password { get; set; }

        public Version HourNerdMusicPlayer_Version { get; internal set; }
        public string HourNerdMusicPlayer_InstallID { get; internal set; }
        public bool HourNerdMusicPlayer_CheckUpdates { get; set; }
        public bool HourNerdMusicPlayer_CheckBetaUpdates { get; set; }
        public bool HourNerdMusicPlayer_RemoteControlEnabled { get; set; }

        public bool HourNerdMusicPlayer_MinimizeToTray { get; set; }
        public bool HourNerdMusicPlayer_ShowTrayNotifications { get; set; }
        public int HourNerdMusicPlayer_Volume { get; set; }
        public bool HourNerdMusicPlayer_PauseOnLock { get; set; }
        public int HourNerdMusicPlayer_MaxHistory { get; set; }

        public bool LastFM_Scrobble { get; set; }
        public string LastFM_SessionKey { get; set; }

        //public bool Misc_ForceSSL { get; set; }

        public Point HourNerdMusicPlayer_StartupLocation { get; set; }
        public Size HourNerdMusicPlayer_StartupSize { get; set; }

        public Dictionary<int,HotkeyConfig> HourNerdMusicPlayer_HotKeys { get; set; }

        public string System_OutputDevice { get; set; }

        public string Appearance_Theme { get; set; }
        public string Current_Color { get; set; }
    }

    public class Config
    {
        public static readonly string HourNerdMusicPlayerAppData =
            DistributionPolicy.ProfileDirectory;

        private readonly MapConfig _c;

        public ConfigFields Fields;
        private readonly string _configFile = "hournerdmusicplayer.config";

        public Config(string configSuffix = "")
        {
            configSuffix = configSuffix.Trim().Replace(" ", "");
            if (configSuffix != "")
            {
                _configFile = "config_" + configSuffix + ".config";
            }

            var appData = (string) ConfigItems.Debug_Logpath.Default;
            if (!Directory.Exists(appData))
                Directory.CreateDirectory(appData);

            string config = Path.Combine(appData, _configFile);

            if (!DistributionPolicy.IsPublicDistribution) MigrateLegacyProfile(config);
            _c = new MapConfig(config);

            Fields.HourNerdMusicPlayer_HotKeys = new Dictionary<int,HotkeyConfig>();

            //If not config file, init with defaults then save
            if (!File.Exists(config))
            {
                LoadConfig();
                SaveConfig();
            }
        }

        private void MigrateLegacyProfile(string destination)
        {
            // Legacy product names are retained solely for backwards compatibility.
            string legacyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elpis");
            string legacyName = _configFile == "hournerdmusicplayer.config" ? "elpis.config" : _configFile;
            ProfileMigration.CopyLegacyProfile(legacyDirectory, Path.GetDirectoryName(destination),
                legacyName, Path.GetFileName(destination));
        }

        public bool LoadConfig()
        {
            if (!_c.LoadConfig())
                return false;

            Fields.Debug_WriteLog = (bool) _c.GetValue(ConfigItems.Debug_WriteLog);
            Fields.Debug_Logpath = (string) _c.GetValue(ConfigItems.Debug_Logpath);
            Fields.Debug_Timestamp = (bool) _c.GetValue(ConfigItems.Debug_Timestamp);

            Fields.Login_Email = (string) _c.GetValue(ConfigItems.Login_Email);
            Fields.Login_Password = _c.GetEncryptedString(ConfigItems.Login_Password);

            Fields.Login_AutoLogin = (bool) _c.GetValue(ConfigItems.Login_AutoLogin);

            Fields.Pandora_AudioFormat = (string) _c.GetValue(ConfigItems.Pandora_AudioFormat);
            if (Fields.Pandora_AudioFormat != PAudioFormat.AACPlus &&
                Fields.Pandora_AudioFormat != PAudioFormat.MP3 &&
                Fields.Pandora_AudioFormat != PAudioFormat.MP3_HIFI)
            {
                Fields.Pandora_AudioFormat = PAudioFormat.MP3;
            }
            Fields.Pandora_AutoPlay = (bool) _c.GetValue(ConfigItems.Pandora_AutoPlay);
            Fields.Pandora_LastStationID = (string) _c.GetValue(ConfigItems.Pandora_LastStationID);
            Fields.Pandora_StationSortOrder = (string) _c.GetValue(ConfigItems.Pandora_StationSortOrder);

            Fields.Logging_LogOnSongLike = (bool) _c.GetValue(ConfigItems.Logging_LogOnSongLike);
            Fields.Logging_LogOnSongLikeFile = (string) _c.GetValue(ConfigItems.Logging_LogOnSongLikeFile); 

            Fields.Proxy_Address = ((string) _c.GetValue(ConfigItems.Proxy_Address)).Trim();
            Fields.Proxy_Port = (int) _c.GetValue(ConfigItems.Proxy_Port);
            Fields.Proxy_User = (string) _c.GetValue(ConfigItems.Proxy_User);
            Fields.Proxy_Password = _c.GetEncryptedString(ConfigItems.Proxy_Password);

            var verStr = (string) _c.GetValue(ConfigItems.HourNerdMusicPlayer_Version);
            Version ver;
            if (Version.TryParse(verStr, out ver))
                Fields.HourNerdMusicPlayer_Version = ver;

            Fields.HourNerdMusicPlayer_InstallID = (string) _c.GetValue(ConfigItems.HourNerdMusicPlayer_InstallID);
            Fields.HourNerdMusicPlayer_CheckUpdates = (bool) _c.GetValue(ConfigItems.HourNerdMusicPlayer_CheckUpdates);
            Fields.HourNerdMusicPlayer_CheckBetaUpdates = (bool)_c.GetValue(ConfigItems.HourNerdMusicPlayer_CheckBetaUpdates);
            Fields.HourNerdMusicPlayer_RemoteControlEnabled = (bool)_c.GetValue(ConfigItems.HourNerdMusicPlayer_RemoteControlEnabled);
            Fields.HourNerdMusicPlayer_MinimizeToTray = (bool) _c.GetValue(ConfigItems.HourNerdMusicPlayer_MinimizeToTray);
            Fields.HourNerdMusicPlayer_ShowTrayNotifications = (bool) _c.GetValue(ConfigItems.HourNerdMusicPlayer_ShowTrayNotifications);
            Fields.HourNerdMusicPlayer_Volume = (int) _c.GetValue(ConfigItems.HourNerdMusicPlayer_Volume);
            Fields.HourNerdMusicPlayer_PauseOnLock = (bool) _c.GetValue(ConfigItems.HourNerdMusicPlayer_PauseOnLock);
            Fields.HourNerdMusicPlayer_MaxHistory = (int) _c.GetValue(ConfigItems.HourNerdMusicPlayer_MaxHistory);

            Fields.LastFM_Scrobble = (bool) _c.GetValue(ConfigItems.LastFM_Scrobble);
            Fields.LastFM_SessionKey = _c.GetEncryptedString(ConfigItems.LastFM_SessionKey);

            var location = (string) _c.GetValue(ConfigItems.HourNerdMusicPlayer_StartupLocation);
            try
            {
                Fields.HourNerdMusicPlayer_StartupLocation = Point.Parse(location);
            }
            catch
            {
                Fields.HourNerdMusicPlayer_StartupLocation = new Point(-1, -1);
            }

            var size = (string) _c.GetValue(ConfigItems.HourNerdMusicPlayer_StartupSize);
            try
            {
                Fields.HourNerdMusicPlayer_StartupSize = Size.Parse(size);
            }
            catch
            {
                Fields.HourNerdMusicPlayer_StartupSize = new Size(0, 0);
            }

            var list = _c.GetValue(ConfigItems.HotKeysList) as Dictionary<int,string>;

            if (list != null){
                foreach (KeyValuePair<int,string> pair in list)
                {
                Fields.HourNerdMusicPlayer_HotKeys.Add(pair.Key,new HotkeyConfig(pair.Value, HotkeyConfig.Default));
                }
            }

            Fields.System_OutputDevice = (string)_c.GetValue(ConfigItems.System_OutputDevice);
            Fields.Appearance_Theme = ThemeManager.NormalizeName((string)_c.GetValue(ConfigItems.Appearance_Theme));
            Fields.Current_Color = (string)_c.GetValue(ConfigItems.Current_Color);

            Log.O("Configuration loaded.");

            return true;
        }

        public HotkeyConfig GetKeyObject(MapConfigEntry entry)
        {
            return new HotkeyConfig((string)_c.GetValue(entry), (HotkeyConfig)entry.Default);
        }

        public bool SaveConfig()
        {
            try
            {
                //TODO: These should be commented out later

                _c.SetValue(ConfigItems.Debug_WriteLog, Fields.Debug_WriteLog);
                _c.SetValue(ConfigItems.Debug_Logpath, Fields.Debug_Logpath);
                _c.SetValue(ConfigItems.Debug_Timestamp, Fields.Debug_Timestamp);
                //*********************************************

                _c.SetValue(ConfigItems.Login_Email, Fields.Login_Email);
                _c.SetEncryptedString(ConfigItems.Login_Password, Fields.Login_Password);
                _c.SetValue(ConfigItems.Login_AutoLogin, Fields.Login_AutoLogin);

                _c.SetValue(ConfigItems.Pandora_AudioFormat, Fields.Pandora_AudioFormat);
                _c.SetValue(ConfigItems.Pandora_AutoPlay, Fields.Pandora_AutoPlay);
                _c.SetValue(ConfigItems.Pandora_LastStationID, Fields.Pandora_LastStationID);
                _c.SetValue(ConfigItems.Pandora_StationSortOrder, Fields.Pandora_StationSortOrder);

                _c.SetValue(ConfigItems.Logging_LogOnSongLike, Fields.Logging_LogOnSongLike);
                _c.SetValue(ConfigItems.Logging_LogOnSongLikeFile, Fields.Logging_LogOnSongLikeFile);

                _c.SetValue(ConfigItems.Proxy_Address, Fields.Proxy_Address.Trim());
                _c.SetValue(ConfigItems.Proxy_Port, Fields.Proxy_Port);
                _c.SetValue(ConfigItems.Proxy_User, Fields.Proxy_User.Trim());
                _c.SetEncryptedString(ConfigItems.Proxy_Password, Fields.Proxy_Password.Trim());

                _c.SetValue(ConfigItems.HourNerdMusicPlayer_Version, Fields.HourNerdMusicPlayer_Version.ToString());
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_CheckUpdates, Fields.HourNerdMusicPlayer_CheckUpdates);
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_CheckBetaUpdates, Fields.HourNerdMusicPlayer_CheckBetaUpdates);
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_RemoteControlEnabled, Fields.HourNerdMusicPlayer_RemoteControlEnabled);
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_MinimizeToTray, Fields.HourNerdMusicPlayer_MinimizeToTray);
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_ShowTrayNotifications, Fields.HourNerdMusicPlayer_ShowTrayNotifications);
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_PauseOnLock, Fields.HourNerdMusicPlayer_PauseOnLock);
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_MaxHistory, Fields.HourNerdMusicPlayer_MaxHistory);

                _c.SetValue(ConfigItems.LastFM_Scrobble, Fields.LastFM_Scrobble);
                _c.SetEncryptedString(ConfigItems.LastFM_SessionKey, Fields.LastFM_SessionKey);


                _c.SetValue(ConfigItems.HourNerdMusicPlayer_StartupLocation, Fields.HourNerdMusicPlayer_StartupLocation.ToString());
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_StartupSize, Fields.HourNerdMusicPlayer_StartupSize.ToString());
                _c.SetValue(ConfigItems.HourNerdMusicPlayer_Volume, Fields.HourNerdMusicPlayer_Volume);


                Dictionary<int, string> hotkeysFlattened = new Dictionary<int, string>();
                foreach(KeyValuePair<int,HotkeyConfig> pair in Fields.HourNerdMusicPlayer_HotKeys)
                {
                    hotkeysFlattened.Add(pair.Key,pair.Value.ToString());
                }
                _c.SetValue(ConfigItems.HotKeysList,hotkeysFlattened);

                _c.SetValue(ConfigItems.System_OutputDevice, Fields.System_OutputDevice);
                _c.SetValue(ConfigItems.Appearance_Theme, Fields.Appearance_Theme);
                _c.SetValue(ConfigItems.Current_Color, Fields.Current_Color);
            }
            catch (Exception ex)
            {
                Log.O("Error saving config: " + ex);
                return false;
            }

            if (!_c.SaveConfig()) return false;

            return true;
        }
    }
}