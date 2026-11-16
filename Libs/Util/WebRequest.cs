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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.ComponentModel;

namespace Util
{
    public class PRequest
    {
        private static WebProxy _proxy;
        private static string _userAgent = "pianobar-2022.04.01";

        public static WebProxy Proxy { get { return _proxy; } }

        public static void SetProxy(string address, string user = "", string password = "")
        {
            var p = new WebProxy(new Uri(address));

            if (user != "")
                p.Credentials = new NetworkCredential(user, password);

            _proxy = p;
        }

        public static void SetProxy(string address, int port, string user = "", string password = "")
        {
            ServicePointManager.Expect100Continue = false;
            var p = new WebProxy(address, port);
           
            if (user != "")
                p.Credentials = new NetworkCredential(user, password);

            _proxy = p;
        }

        public static string StringRequest(string url, string data)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "text/plain; charset=utf-8";
            request.UserAgent = _userAgent;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.AllowAutoRedirect = false;
            request.ServicePoint.Expect100Continue = false;
            if (_proxy != null) request.Proxy = _proxy;
            byte[] payload = Encoding.UTF8.GetBytes(data);
            request.ContentLength = payload.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(payload, 0, payload.Length);
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                    throw new WebException("Unexpected HTTP status.", null,
                        WebExceptionStatus.ProtocolError, response);
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        public static void ByteRequestAsync(string url, DownloadDataCompletedEventHandler dataHandler)
        {
            Log.O("Downloading Async: " + url);
            var wc = new WebClient();
            if (_proxy != null)
                wc.Proxy = _proxy;

            wc.DownloadDataCompleted += dataHandler;
            wc.DownloadDataAsync(new Uri(url));
        }

        public static byte[] ByteRequest(string url)
        {
            return ByteRequest(url, 8000);
        }

        public static byte[] ByteRequest(string url, int timeoutMilliseconds)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = timeoutMilliseconds;
            request.ReadWriteTimeout = timeoutMilliseconds;
            if (_proxy != null) request.Proxy = _proxy;
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var output = new MemoryStream())
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }

        public static void FileRequest(string url, string outputFile)
        {
            var wc = new WebClient();
            if (_proxy != null)
                wc.Proxy = _proxy;
            wc.DownloadFile(url, outputFile);
        }

        public static void FileRequestAsync(string url, string outputFile,
            DownloadProgressChangedEventHandler progressCallback,
            AsyncCompletedEventHandler completeCallback)
        {
            var wc = new WebClient();
            if (_proxy != null)
                wc.Proxy = _proxy;

            wc.DownloadFileCompleted += completeCallback;
            wc.DownloadProgressChanged += progressCallback;
            wc.DownloadFileAsync(new System.Uri(url), outputFile);
        }
    }
}