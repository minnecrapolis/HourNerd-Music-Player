/*
 * Copyright 2012 - Adam Haile
 * http://adamhaile.net
 *
 * This file is part of PandoraSharp.
 * PandoraSharp is free software: you can redistribute it and/or modify 
 * it under the terms of the GNU General Public License as published by 
 * the Free Software Foundation, either version 3 of the License, or 
 * (at your option) any later version.
 * 
 * PandoraSharp is distributed in the hope that it will be useful, 
 * but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License 
 * along with PandoraSharp. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PandoraSharp.Exceptions;
using Util;
using Newtonsoft.Json.Linq;

namespace PandoraSharp
{
    public class Pandora
    {
        #region Delegates

        public delegate void ConnectionEventHandler(object sender, bool state, ErrorCodes code);

        public delegate void FeedbackUpdateEventHandler(object sender, Song song, bool success);

        public delegate void LoginStatusEventHandler(object sender, string status);

        public delegate void PandoraErrorEventHandler(object sender, string errorCode, string msg);

        public delegate void StationsUpdatedEventHandler(object sender);

        public delegate void StationsUpdatingEventHandler(object sender);

        public delegate void QuickMixSavedEventHandler(object sender);

        #endregion

        #region SortOrder enum

        public enum SortOrder
        {
            DateAsc,
            DateDesc,
            AlphaAsc,
            AlphaDesc,
            RatingAsc,
            RatingDesc
        }

        #endregion

        private readonly object _sessionLock = new object();
        private readonly object _authTokenLock = new object();
        private readonly object _partnerIDLock = new object();
        private readonly object _userIDLock = new object();
        private readonly object _rpcCountLock = new object();

        protected internal List<string> QuickMixStationIDs = new List<string>();
        private string _audioFormat = PAudioFormat.MP3;

        private string _authToken;
        private string _partnerID;
        private string _userID;

        private bool _authorizing;
        private bool _connected;
        private bool _firstAuthComplete = false;
        private string _imageCachePath = "";
        private string _password = "";
        private string _rid;
        private int _rpcCount;
        private long _syncTime;
        private long _timeSynced;
        private bool _metaDataUpToDate;

        private string _user = "";
        private string listenerId;
        //private string webAuthToken;

        public Pandora()
        {
            QuickMixStationIDs = new List<string>();
            StationSortOrder = SortOrder.DateDesc;
            HasSubscription = true;
            //this.set_proxy(null);
        }

        private string AuthToken
        {
            get
            {
                lock (_authTokenLock)
                {
                    return _authToken;
                }
            }
            set
            {
                lock (_authTokenLock)
                {
                    _authToken = value;
                }
            }
        }

        private string PartnerID
        {
            get
            {
                lock (_partnerIDLock)
                {
                    return _partnerID;
                }
            }
            set
            {
                lock (_partnerIDLock)
                {
                    _partnerID = value;
                }
            }
        }

        private string UserID
        {
            get
            {
                lock (_userIDLock)
                {
                    return _userID;
                }
            }
            set
            {
                lock (_userIDLock)
                {
                    _userID = value;
                }
            }
        }

        public List<Station> Stations { get; private set; }

        public string ImageCachePath
        {
            get { return _imageCachePath; }
            set { _imageCachePath = value; }
        }

        [DefaultValue(true)]
        public bool HasSubscription { get; private set; }

        public string AudioFormat
        {
            get { return _audioFormat; }
            set { SetAudioFormat(value); }
        }

        private bool _forceSSL = true;
        public bool ForceSSL
        {
            get { return _forceSSL; }
            set { _forceSSL = true; } // API requests always require HTTPS.
        }

        public SortOrder StationSortOrder { get; set; }
        public event ConnectionEventHandler ConnectionEvent;
        public event StationsUpdatedEventHandler StationUpdateEvent;
        public event StationsUpdatingEventHandler StationsUpdatingEvent;
        public event FeedbackUpdateEventHandler FeedbackUpdateEvent;
        public event LoginStatusEventHandler LoginStatusEvent;
        public event QuickMixSavedEventHandler QuickMixSavedEvent;

        protected internal virtual string RPCRequest(string url, string data)
        {
            try
            {
                return PRequest.StringRequest(url, data);
            }
            catch (WebException e)
            {
                ErrorCodes code = ErrorCodes.ERROR_RPC;
                var response = e.Response as HttpWebResponse;
                if (response != null)
                {
                    int status = (int)response.StatusCode;
                    Log.O("Pandora HTTP status: " + status);
                    if (status == 401 || status == 403) code = ErrorCodes.API_ACCESS_DENIED;
                    else if (status == 429) code = ErrorCodes.API_RATE_LIMITED;
                    response.Close();
                }
                else if (e.Status == WebExceptionStatus.Timeout) code = ErrorCodes.API_TIMEOUT;
                else if (e.Status == WebExceptionStatus.TrustFailure ||
                         e.Status == WebExceptionStatus.SecureChannelFailure) code = ErrorCodes.API_TLS_ERROR;
                Log.O("Pandora transport failure: " + e.Status);
                // WebException text can contain the authenticated URL. Do not log it.
                throw new PandoraException(code);
            }
            catch (Exception e)
            {
                Log.O("Pandora transport failure: " + e.GetType().Name);
                throw new PandoraException(ErrorCodes.ERROR_RPC);
            }
        }

        //Checks for fault returns.  If it's an Auth fault (auth timed out)
        //return false, which signals that a re-auth and retry needs to be done
        //otherwise return true signalling all clear.
        //All other faults will be thrown
        protected internal bool HandleFaults(JSONResult result, bool secondTry)
        {
            if (result.Fault)
            {
                if (result.FaultCode == ErrorCodes.INVALID_AUTH_TOKEN)
                    if (!secondTry)
                        return false; //auth fault, signal a re-auth

                Log.O("Pandora fault code: " + (int)result.FaultCode);
                throw new PandoraException(result.FaultCode); //other, throw the exception
            }

            return true; //no fault
        }

        protected internal string CallRPC_Internal(string method, JObject request, 
            bool isAuth, bool useSSL = false)
        {
            int callID = 0;
            lock (_rpcCountLock)
            {
                callID = _rpcCount++;
            }

            string shortMethod = (method.Contains("&") ? 
                method.Substring(0, method.IndexOf("&")) : method);

            string url = "https://" + Const.RPC_URL + "?method=" + method;

            if (request == null) request = new JObject();

            if(AuthToken != null && 
                PartnerID != null)
            {
                //if (!url.EndsWith("?")) url += "?";
                url += ("&partner_id=" + PartnerID);
                url += ("&auth_token=" + Uri.EscapeDataString(AuthToken));

                if (UserID != null)
                {
                    url += ("&user_id=" + UserID);
                    request["userAuthToken"] = AuthToken;
                    request["syncTime"] = AdjustedSyncTime();
                }
            }

            string json = request.ToString();
            string data = string.Empty;
            if(method == "auth.partnerLogin")
                data = json;
            else
                data = Crypto.out_key.Encrypt(json);

            // Log method and stage only: URLs, payloads and responses carry credentials/tokens.
            Log.O("[" + callID + "]: " + shortMethod);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try { return RPCRequest(url, data); }
            finally { Log.O("API " + shortMethod + ": " + watch.ElapsedMilliseconds + " ms"); }
        }

        protected internal JSONResult CallRPC(string method, JObject request = null, 
                                          bool isAuth = false, bool useSSL = false)
        {
            // Keep tokens and the complete partner/user login pair consistent across threads.
            lock (_sessionLock)
            {
                JSONResult result = new JSONResult(CallRPC_Internal(method, request, isAuth, useSSL));
                if (!result.Fault) return result;
                if (isAuth || result.FaultCode != ErrorCodes.INVALID_AUTH_TOKEN)
                {
                    HandleFaults(result, true);
                    return result;
                }

                Log.O("Session expired; renewing once.");
                if (!AuthenticateUser())
                    throw new PandoraException(ErrorCodes.INVALID_AUTH_TOKEN);

                // Parse and return the NEW response, and never retry a second time.
                result = new JSONResult(CallRPC_Internal(method, request, isAuth, useSSL));
                HandleFaults(result, true);
                return result;
            }
        }

        protected internal JSONResult CallRPC(string method, params object[] args)
        {
            JObject req = new JObject();
            if (args.Length % 2 != 0)
            {
                Log.O("CallRPC: Called with an uneven number of arguments!");
                return null;
            } 

            for (int i=0; i < args.Length; i+=2)
            {
                if(args[i].GetType() != typeof(string) || args[i].GetType() != typeof(String))
                {
                    Log.O("CallRPC: Called with an incorrect parameter type!");
                    return null;
                }
                req[(string)args[i]] = JToken.FromObject(args[i + 1]);
            }

            return CallRPC(method, req);
        }

        protected internal object CallRPC(string method, object[] args, bool b_url_args = false,
                                          bool isAuth = false, bool useSSL = false, bool insertTime = true)
        {
            return null;
        }

        public void RefreshStations()
        {
            Log.O("RefreshStations");
            if (StationsUpdatingEvent != null)
                StationsUpdatingEvent(this);

            JObject req = new JObject();
            req["includeStationArtUrl"] = true;
            var stationList = CallRPC("user.getStationList", req);

            QuickMixStationIDs.Clear();

            Stations = new List<Station>();
            var stations = stationList.Result["stations"];
            foreach (JToken d in stations)
            {
                Stations.Add(new Station(this, d));
            }
            //foreach (PDict s in stationList)
            //    Stations.Add(new Station(this, s));

            if (QuickMixStationIDs.Count > 0)
            {
                foreach (Station s in Stations)
                {
                    if (QuickMixStationIDs.Contains(s.ID))
                        s.UseQuickMix = true;
                }
            }

            List<Station> quickMixes = Stations.FindAll(x => x.IsQuickMix);
            Stations = Stations.FindAll(x => !x.IsQuickMix);

            switch (StationSortOrder)
            {
                case SortOrder.DateDesc:
                    //Stations = Stations.OrderByDescending(x => x.ID).ToList();
                    Stations = Stations.OrderByDescending(x => Convert.ToInt64(x.ID)).ToList();
                    break;
                case SortOrder.DateAsc:
                    //Stations = Stations.OrderBy(x => x.ID).ToList();
                    Stations = Stations.OrderBy(x => Convert.ToInt64(x.ID)).ToList();
                    break;
                case SortOrder.AlphaDesc:
                    Stations = Stations.OrderByDescending(x => x.Name).ToList();
                    break;
                case SortOrder.AlphaAsc:
                    Stations = Stations.OrderBy(x => x.Name).ToList();
                    break;
                case SortOrder.RatingAsc:
                    GetStationMetaData();
                    Stations = Stations.OrderBy(x => x.ThumbsUp).ToList();
                    break;
                case SortOrder.RatingDesc:
                    GetStationMetaData();
                    Stations = Stations.OrderByDescending(x => x.ThumbsUp).ToList();
                    break;

            }

            Stations.InsertRange(0, quickMixes);

            if (StationUpdateEvent != null)
                StationUpdateEvent(this);
        }

        //private string getSyncKey()
        //{
        //    string result = string.Empty;

        //    try
        //    {
        //        var keyArray = new Util.Downloader().DownloadString(Const.SYNC_KEY_URL);

        //        var vals = keyArray.Split('|');
        //        if (vals.Length < 3) return result;
        //        var len = 48;
        //        if (!Int32.TryParse(vals[1], out len)) return result;
        //        if (vals[2].Length != len) return result;

        //        Log.O("Sync Key Age (sec): " + vals[0]);
        //        Log.O("Sync Key Length: " + vals[1]);
        //        Log.O("Sync Key: " + vals[2]);

        //        result = vals[2];
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.O(ex.ToString());
        //    }

        //    return result;
        //}

        //private string getSyncTime()
        //{
        //    string result = string.Empty;

        //    try
        //    {
        //        result = new Util.Downloader().DownloadString(Const.SYNC_TIME_URL);
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.O(ex.ToString());
        //    }

        //    return result;
        //}

        public void Logout()
        {
            lock (_sessionLock)
            {
                _firstAuthComplete = false;
                _connected = false;
                AuthToken = null;
                PartnerID = null;
                UserID = null;
                _user = string.Empty;
                _password = string.Empty;
            }
        }

        public long AdjustedSyncTime()
        {
            return _syncTime + (Time.Unix() - _timeSynced);
        }

        public bool AuthenticateUser()
        {
            lock (_sessionLock)
            {
                _authorizing = true;
                try
                {
                    listenerId = null;
                    AuthToken = null;
                    PartnerID = null;
                    UserID = null;
                    Log.O("Login stage: partner authentication");
                    SendLoginStatus("Connecting to Pandora...");

                    JObject req = new JObject();
                    req["username"] = "android";
                    string partnerPassword = GetPartnerPassword();
                    if (!HasConnectionConfiguration)
                        throw new PandoraException(ErrorCodes.PARTNER_CONFIGURATION_REQUIRED);
                    req["password"] = partnerPassword;
                    req["deviceModel"] = "android-generic";
                    req["version"] = "5";
                    req["includeUrls"] = true;
                    JSONResult ret = CallRPC("auth.partnerLogin", req, true, true);
                    JToken result = ret.Result;
                    RequireFields(result, "syncTime", "partnerId", "partnerAuthToken");
                    try
                    {
                        _syncTime = Crypto.DecryptSyncTime(result["syncTime"].ToString());
                    }
                    catch (Exception)
                    {
                        throw new PandoraException(ErrorCodes.API_INVALID_RESPONSE);
                    }
                    _timeSynced = Time.Unix();
                    PartnerID = result["partnerId"].ToString();
                    AuthToken = result["partnerAuthToken"].ToString();

                    Log.O("Login stage: user authentication");
                    SendLoginStatus("Signing in to Pandora...");
                    req = new JObject();
                    req["loginType"] = "user";
                    req["username"] = _user;
                    req["password"] = _password;
                    req["includePandoraOneInfo"] = true;
                    req["includeAdAttributes"] = true;
                    req["includeSubscriptionExpiration"] = true;
                    req["partnerAuthToken"] = AuthToken;
                    req["syncTime"] = AdjustedSyncTime();
                    ret = CallRPC("auth.userLogin", req, true, true);
                    result = ret.Result;
                    RequireFields(result, "userAuthToken", "userId", "hasAudioAds");
                    AuthToken = result["userAuthToken"].ToString();
                    UserID = result["userId"].ToString();
                    HasSubscription = !result["hasAudioAds"].ToObject<bool>();
                    return true;
                }
                catch
                {
                    AuthToken = null;
                    PartnerID = null;
                    UserID = null;
                    throw;
                }
                finally
                {
                    _authorizing = false;
                }
            }
        }

        private static string PartnerPasswordPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HourNerdMusicPlayer", "Connection", "PandoraPartner.dat"); }
        }

        // The integration password is supplied locally; public builds contain no default.
        public static string GetPartnerPassword() { return GetConnectionValue("Password"); }

        public static bool HasConnectionConfiguration
        {
            get { return !string.IsNullOrWhiteSpace(GetPartnerPassword()) &&
                ValidProtocolKey(GetConnectionValue("IncomingKey")) &&
                ValidProtocolKey(GetConnectionValue("OutgoingKey")); }
        }

        private static bool ValidProtocolKey(string value)
        {
            int length = Encoding.UTF8.GetByteCount(value ?? string.Empty);
            return length >= 4 && length <= 56;
        }

        public static string GetConnectionValue(string name)
        {
            string suffix = name == "Password" ? "PARTNER_PASSWORD" :
                name == "IncomingKey" ? "INCOMING_KEY" : "OUTGOING_KEY";
            string configured = Environment.GetEnvironmentVariable("HOURNERD_PANDORA_" + suffix);
            if (configured != null) return configured.Trim();
            try
            {
                if (!File.Exists(PartnerPasswordPath)) return string.Empty;
                byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(PartnerPasswordPath), null,
                    DataProtectionScope.CurrentUser);
                try { return (string)JObject.Parse(Encoding.UTF8.GetString(clear))[name] ?? string.Empty; }
                finally { Array.Clear(clear, 0, clear.Length); }
            }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
            catch (CryptographicException) { return string.Empty; }
            catch (Newtonsoft.Json.JsonException) { return string.Empty; }
        }

        public static void SaveConnection(string password, string incomingKey, string outgoingKey)
        {
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Enter a connection password.");
            incomingKey = (incomingKey ?? string.Empty).Trim();
            outgoingKey = (outgoingKey ?? string.Empty).Trim();
            if (!ValidProtocolKey(incomingKey) || !ValidProtocolKey(outgoingKey))
                throw new ArgumentException("Protocol keys must contain between 4 and 56 UTF-8 bytes.");
            var settings = new JObject();
            settings["Password"] = password.Trim();
            settings["IncomingKey"] = incomingKey;
            settings["OutgoingKey"] = outgoingKey;
            byte[] clear = Encoding.UTF8.GetBytes(settings.ToString(Newtonsoft.Json.Formatting.None));
            try
            {
                byte[] encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
                Directory.CreateDirectory(Path.GetDirectoryName(PartnerPasswordPath));
                File.WriteAllBytes(PartnerPasswordPath, encrypted);
                foreach (string suffix in new[] { "PARTNER_PASSWORD", "INCOMING_KEY", "OUTGOING_KEY" })
                    Environment.SetEnvironmentVariable("HOURNERD_PANDORA_" + suffix, null);
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }

        private static void RequireFields(JToken result, params string[] fields)
        {
            if (!(result is JObject) || fields.Any(field => result[field] == null ||
                result[field].Type == JTokenType.Null || string.IsNullOrEmpty(result[field].ToString())))
                throw new PandoraException(ErrorCodes.API_INVALID_RESPONSE);
        }

        private void SendLoginStatus(string status)
        {
            if (LoginStatusEvent != null)
                LoginStatusEvent(this, status);
        }

        public void Connect(string user, string password)
        {
            Log.O("Connect");
            ErrorCodes status = ErrorCodes.SUCCESS;
            _connected = false;

            _user = user;
            _password = password;

            try
            {
                SendLoginStatus("Authenticating with Pandora...");
                _connected = AuthenticateUser();

                if (_connected)
                {
                    SendLoginStatus("Loading station list...");
                    RefreshStations();
                }
                else
                {
                    status = ErrorCodes.ERROR_RPC;
                }
            }
            catch (PandoraException ex)
            {
                status = ex.Fault;
                _connected = false;
            }
            catch (Exception ex)
            {
                status = ErrorCodes.UNKNOWN_ERROR;
                Log.O("Connection failure: " + ex.GetType().Name);
                _connected = false;
            }


            if (ConnectionEvent != null)
                ConnectionEvent(this, _connected, status);
        }

        //public void SetProxy()
        //{

        //}

        public void SetAudioFormat(string fmt)
        {
            if ((fmt != PAudioFormat.AACPlus &&
                 fmt != PAudioFormat.MP3 &&
                 fmt != PAudioFormat.MP3_HIFI) ||
                (!HasSubscription && fmt == PAudioFormat.MP3_HIFI))
            {
                fmt = PAudioFormat.MP3;
            }

            _audioFormat = fmt;
        }

        public void SaveQuickMix()
        {
            var ids = new List<string>();
            foreach (Station s in Stations)
            {
                if (s.UseQuickMix)
                    ids.Add(s.ID);
            }

            JObject req = new JObject();
            req["quickMixStationIds"] = new JArray(ids.ToArray());

            CallRPC("user.setQuickMix", req);

            if (QuickMixSavedEvent != null)
                QuickMixSavedEvent(this);
        }

        public List<SearchResult> Search(string query)
        {
            Log.O("Search: " + query);
            JObject req = new JObject();
            req["searchText"] = query;
            var search = CallRPC("music.search", req);

            var list = new List<SearchResult>();
            var artists = search.Result["artists"];
            var songs = search.Result["songs"];
            foreach (JToken a in artists)
                list.Add(new SearchResult(SearchResultType.Artist, a));

            foreach (JToken s in songs)
                list.Add(new SearchResult(SearchResultType.Song, s));

            list = list.OrderByDescending((i) => i.Score).ToList();

            return list;
        }

        public Station CreateStationFromSearch(string token)
        {
            JObject req = new JObject();
            req["musicToken"] = token;
            var result = CallRPC("station.createStation", req);

            var station = new Station(this, result.Result);
            Stations.Add(station);

            return station;
        }

        private Station CreateStation(Song song, string type)
        {
            JObject req = new JObject();
            req["trackToken"] = song.TrackToken;
            req["musicType"] = type;
            var result = CallRPC("station.createStation", req);

            var station = new Station(this, result.Result);
            Stations.Add(station);

            return station;
        }

        private void GetStationMetaData()
        {
            Log.O("RetrieveStationMetaData");

            Parallel.ForEach(Stations, station =>
            {
                JObject req = new JObject();

                req["stationToken"] = station.IdToken;
                req["includeExtendedAttributes"] = true;
                var stationInfo = CallRPC("station.getStation", req);

                var feedback = stationInfo.Result["feedback"];

                station.ThumbsUp = Convert.ToInt32(feedback["totalThumbsUp"].ToString());
                station.ThumbsDown = Convert.ToInt32(feedback["totalThumbsDown"].ToString());
            });
        } 

        public Station CreateStationFromSong(Song song)
        {
            return CreateStation(song, "song");
        }

        public Station CreateStationFromArtist(Song song)
        {
            return CreateStation(song, "artist");
        }

        public void AddFeedback(string stationToken, string trackToken, SongRating rating)
        {
            Log.O("AddFeedback");

            bool rate = (rating == SongRating.love) ? true : false;

            JObject req = new JObject();
            req["stationToken"] = stationToken;
            req["trackToken"] = trackToken;
            req["isPositive"] = rate;

            CallRPC("station.addFeedback", req);
        }

        public void DeleteFeedback(string feedbackID)
        {
            Log.O("DeleteFeedback");

            object result = CallRPC("station.deleteFeedback", "feedbackId", feedbackID);
        }

        public void CallFeedbackUpdateEvent(Song song, bool success)
        {
            if (FeedbackUpdateEvent != null)
                FeedbackUpdateEvent(this, song, success);
        }

        public Station GetStationByID(string id)
        {
            foreach (Station s in Stations)
            {
                if (s.ID == id)
                    return s;
            }

            return null;
        }

        public string GetFeedbackID(string stationToken, string trackToken)
        {
            JObject req = new JObject();
            req["stationToken"] = stationToken;
            req["trackToken"] = trackToken;
            req["isPositive"] = true;

            var feedback = CallRPC("station.addFeedback", req);
            return (string)feedback.Result["feedbackId"];
        }
    }
}