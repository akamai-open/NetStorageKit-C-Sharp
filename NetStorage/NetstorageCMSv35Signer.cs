// Copyright 2014 Akamai Technologies http://developer.akamai.com.
//
// Licensed under the Apache License, KitVersion 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Author: colinb@akamai.com  (Colin Bendell)
//

using Akamai.Auth;
using Akamai.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;

namespace Akamai.Netstorage
{
 
    /// <summary>
    /// The API Signer is responsible for brokering a singular APIParams.
    /// An event is a transaction with CMS API. This class is responsible for the core 
    /// interaction logic given an API command and the associated set of parameters.
    /// 
    /// When event is executed, three headers are added to the http output:
    /// X-Akamai-ACS-Action: 
    /// X-Akamai-ACS-Auth-Data: 
    /// X-Akamai-ACS-Auth-Sign: 
    /// 
    /// If connection is going to be reused, pass the persistent HttpWebRequest object when calling execute()
    /// 
    /// TODO: support rebinding on IO communication errors (eg: connection reset)
    /// TODO: support Async callbacks and Async IO
    /// TODO: support multiplexing of uploads
    /// TODO: optimize and adapt throughput based on connection latency
    /// TODO: support HTTP trailers for late SHA256 validation
    /// 
    /// Author: colinb@akamai.com  (Colin Bendell)
    /// </summary>
    public class NetstorageCMSv35Signer : IRequestSigner
    {
        public class SignType
        {
            public static SignType HMACMD5 = new SignType(3, KeyedHashAlgorithm.HMACMD5);
            public static SignType HMACSHA1 = new SignType(4, KeyedHashAlgorithm.HMACSHA1);
            public static SignType HMACSHA256 = new SignType(5, KeyedHashAlgorithm.HMACSHA256);

            public int VersionID { get; private set; }
            public KeyedHashAlgorithm Algorithm { get; private set; }
            private SignType(int versionID, KeyedHashAlgorithm algorithm)
            {
                this.VersionID = versionID;
                this.Algorithm = algorithm;
            }
        }
        public class HashType
        {
            public static HashType MD5= new HashType(ChecksumAlgorithm.MD5);
            public static HashType SHA1 = new HashType(ChecksumAlgorithm.SHA1);
            public static HashType SHA256 = new HashType(ChecksumAlgorithm.SHA256);

            public ChecksumAlgorithm Checksum { get; private set; }
            private HashType(ChecksumAlgorithm checksum)
            {
                this.Checksum = checksum;
            }
        }

        private const string KitVersion = "CSharp/3.51";
        private const string KitVersionHeader = "X-Akamai-NSKit";


        public const string ActionHeader = "X-Akamai-ACS-Action";
        public const string AuthDataHeader = "X-Akamai-ACS-Auth-Data";
        public const string AuthSignHeader = "X-Akamai-ACS-Auth-Sign";



        public string Method { get; set; }
        public Uri URI { get; set; }
        public string Username { get; private set; }
        public string Secret { get; private set; }
        public APIParams Params { get; set; }
        public Stream UploadStream { get; set; }
        public SignType SignVersion { get; set; }

        public NetstorageCMSv35Signer(string method, Uri uri, string username, string secret, APIParams apiParams, Stream uploadStream = null)
        {
            this.Method = method;
            this.URI = uri;
            this.Params = apiParams;
            this.Username = username;
            this.Secret = secret;
            this.UploadStream = uploadStream;
            this.SignVersion = SignType.HMACSHA256;
        }

        /// <summary>
        /// All Netstorage parameters have lowercase names
        /// </summary>
        internal static string ParamsNameFormatter(string s) { return s.ToLower(); }

        /// <summary>
        /// Custom Formatter for API Event. Of particular interest is:
        /// DateTime - formatted as a (long) which is represented as seconds since epoch
        /// byte[] - formatted as hex
        /// bool - formatted as 1 or 0
        /// </summary>
        internal static string ParamsValueFormatter(object o)
        {
            if (o is byte[])
                return ((byte[])o).ToHex().ToLower();
            else if (o is DateTime)
                return ((DateTime)o).GetEpochSeconds().ToString();
            else if (o is bool)
                return Convert.ToInt32((bool)o).ToString();

            // Default to the format provided by the argument to format.
            if (o != null)
                return o.ToString();
            else
                return string.Empty;
        }

        internal string GetActionHeaderValue()
        {
            return this.Params.ConvertToQueryString(ParamsNameFormatter, ParamsValueFormatter);
        }

        internal string GetAuthDataHeaderValue()
        {
            return string.Format("{0}, 0.0.0.0, 0.0.0.0, {1}, {2}, {3}", 
                this.SignVersion.VersionID, DateTime.UtcNow.GetEpochSeconds(), new Random().Next(), this.Username);
        }

        internal string GetAuthSignHeaderValue(string action, string authData)
        {
            byte[] signData = string.Format("{0}{1}\n{2}:{3}\n", 
                authData, this.URI.AbsolutePath, NetstorageCMSv35Signer.ActionHeader.ToLower(), action).ToByteArray();

            return signData.ComputeKeyedHash(this.Secret, this.SignVersion.Algorithm).ToBase64();
        }

        public WebHeaderCollection ComputeHeaders()
        {
            string action = GetActionHeaderValue();
            string authData = GetAuthDataHeaderValue();
            string authSign = GetAuthSignHeaderValue(action, authData);

            return new WebHeaderCollection() 
            {
                {NetstorageCMSv35Signer.KitVersionHeader, NetstorageCMSv35Signer.KitVersion},
                {NetstorageCMSv35Signer.ActionHeader, action}, 
                {NetstorageCMSv35Signer.AuthDataHeader, authData}, 
                {NetstorageCMSv35Signer.AuthSignHeader, authSign}
            };
        }

        /// <summary>
        /// Validates the response and attempts to detect root causes for failures for non 200 responses. The most common cause is 
        /// due to time synchronization of the local server. If the local server is more than 30seconds out of sync then the 
        /// API server will reject the request.
        /// 
        /// TODO: catch rate limitting errors. Should delay and retry.
        /// </summary>
        /// <param name="response">the active response object</param>
        public void Validate(WebResponse response)
        {
            if (response is HttpWebResponse)
            {
                HttpWebResponse httpResponse = (HttpWebResponse)response;
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                    return;

                DateTime responseDate;
                string date = response.Headers.Get("Date");
                if (date != null 
                    && DateTime.TryParse(date, out responseDate))
                    if (DateTime.Now.Subtract(responseDate).TotalSeconds > 30)
                        throw new HttpRequestException("Local server Date is more than 30s out of sync with Remote server");

                throw new HttpRequestException(string.Format("Unexpected Response from Server: {0} {1}\n{2}", httpResponse.StatusCode, httpResponse.StatusDescription, response.Headers));
            }
        }

        /// <summary>
        /// Opens the connection to Netstorage, assembles the signing headers and uploads any files.
        /// </summary>
        /// <param name="request">the </param>
        /// <returns> the output stream of the response</returns>
        public Stream Execute(WebRequest request = null)
        {
            //Make sure that this connection will behave nicely with multiple calls in a connection pool.
            ServicePointManager.EnableDnsRoundRobin = true;

            request = Sign(request);
            if (this.Method == "PUT" || this.Method == "POST")
            {
                //Disable the nastiness of Expect100Continue
                ServicePointManager.Expect100Continue = false;
                //Another hack to avoid problems with the read timeout even though the 
                //bytes are being sent to the client. .NET doesn't distinguish between
                //a read timeout and a writetimeout.
                request.Timeout = System.Threading.Timeout.Infinite;


                if (this.UploadStream == null)
                    request.ContentLength = 0;
                else if (this.UploadStream.CanSeek)
                    request.ContentLength = this.UploadStream.Length;
                else if (request is HttpWebRequest)
                    ((HttpWebRequest)request).SendChunked = true;

                if (this.UploadStream != null)
                {
                    // avoid internal memory allocation before buffering the output
                    if (request is HttpWebRequest)
                        ((HttpWebRequest)request).AllowWriteStreamBuffering = false;

                    using (Stream requestStream = request.GetRequestStream())
                    using (this.UploadStream)
                    {
                        UploadStream.Position = 0;
                        this.UploadStream.CopyTo(requestStream, 32 * 1024);
                    }
                }
            }

            WebResponse response = null;
            try
            {
                response = request.GetResponse();
            }
            catch (WebException e)
            {
                // non 200 OK responses throw exceptions.
                // is this because of Time drift? can we re-try?
                using (response = e.Response)
                    Validate(response);
            }

            return response.GetResponseStream();
        }

        /// <summary>
        /// Add the headers for signing a WebRequest
        /// </summary>
        /// <param name="request">add to an existing webrequest or create based on the uri</param>
        /// <returns></returns>
        public WebRequest Sign(WebRequest request = null)
        {
            if (request == null)
                request = (WebRequest) WebRequest.Create(this.URI);

            request.Method = this.Method;
            //request.AllowAutoRedirect = false;
            
            request.Headers.Add(ComputeHeaders());
            return request;
        }
    }

    /// <summary>
    /// The APIParams represents all the possible parameters for the CMS API
    /// 
    /// Verion: always 1
    /// Action: the cms action (eg: "dir", "upload", etc)
    /// Format: for actions that return content, defines the format types (eg: "xml")
    /// QuickDelete: always "imreallyreallysure"
    /// Destination: URI Path for the rename
    /// Target: URI Path of the existing file/dir for a symlink
    /// MTime: modified time
    /// Size: byte size of an uploaded file. NB: do not specify if indexing a zip
    /// MD5: MD5 checksum
    /// SHA1: SHA1 checksum
    /// SHA256: SHA256 checksum
    /// InxedZip: True if the zip file is to be enabled for serve from zip functionality
    /// </summary>
    public class APIParams
    {
        public int Version { get { return 1; } }
        public string Action { get; set; }
        public string Format { get; set; }

        [DataMember(Name ="quick-delete")]
        public string QuickDelete { get; set; }
        public string Destination { get; set; }
        public string Target { get; set; }
        public DateTime? MTime { get; set; }
        public long? Size { get; set; }
        public byte[] MD5 { get; set; }
        public byte[] SHA1 { get; set; }
        public byte[] SHA256 { get; set; }
        [DataMember(Name = "index-zip")]
        public bool? IndexZip { get; set; }
    }
}
