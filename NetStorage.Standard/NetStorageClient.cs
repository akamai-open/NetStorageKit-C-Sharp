// Copyright 2018 Derivco Estonia
//
// Licensed under the Apache License, Version 2.0 (the "License");
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
// Author: meelis.talvis@derivco.ee  (Meelis Talvis)
// Contributor: colinb@akamai.com  (Colin Bendell)
//

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NetStorage.Standard.Models;
using Polly;

namespace NetStorage.Standard
{
  public class NetStorageClient : HttpClient
  {
    public Uri Uri { get; set; }
    public const string ActionHeader = "X-Akamai-ACS-Action";
    public const string AuthDataHeader = "X-Akamai-ACS-Auth-Data";
    public const string AuthSignHeader = "X-Akamai-ACS-Auth-Sign";

    public NetStorageCredentials Credentials { get; set; }
    public SignType SignVersion { get; set; }
    public APIParams Params { get; set; }

    public NetStorageClient(NetStorageCredentials credentials)
    {
      SignVersion = SignType.HMACSHA256;
      Credentials = credentials;
    }

    public NetStorageClient(NetStorageCredentials credentials, HttpMessageHandler handler) : base(handler)
    {
      SignVersion = SignType.HMACSHA256;
      Credentials = credentials;
    }

    public NetStorageClient(NetStorageCredentials credentials, HttpMessageHandler handler, bool disposeHandler) : base(handler, disposeHandler)
    {
      SignVersion = SignType.HMACSHA256;
      Credentials = credentials;
    }

    /// <summary>
    /// Constructs the full Net Storage URI with the host name as the prefix to the path
    /// </summary>
    /// <param name="path">/[CP Code]/[Path]</param>
    /// <returns>Complete Net Storage URI</returns>
    public async Task<Uri> GetNetStorageUri(string path)
    {
      return await Task.FromResult(
        new UriBuilder {Scheme = Credentials.UseSSL ? "HTTPS" : "HTTP", Host = Credentials.HostName, Path = path}
          .Uri);
    }

    /// <summary>
    /// Creates the Action header for the request
    /// NB! NetStorageClient Params value has to be set first!
    /// </summary>
    /// <returns>Action header</returns>
    public async Task<string> CreateActionHeader()
    {
      if (Params == null)
        throw new NullReferenceException($"{nameof(Params)} has to be set before calling this method!");

      return await Task.FromResult(Params.ConvertToQueryString(Signer.ParamsNameFormatter,
        Signer.ParamsValueFormatter));
    }

    /// <summary>
    /// Creates the Auth-Data header for the request
    /// </summary>
    /// <returns>Auth-Data header</returns>
    public async Task<string> CreateAuthDataHeader()
    {
      return await Task.FromResult(
        $"{SignVersion.VersionID}, 0.0.0.0, 0.0.0.0, {DateTime.UtcNow.GetEpochSeconds()}, {new Random().Next()}, {Credentials.Username}");
    }

    /// <summary>
    /// Uses the previously generated Action and Auth-Data headers to create the Auth-Sign header for the request
    /// </summary>
    /// <param name="action">Action header</param>
    /// <param name="authData">Auth-Data header</param>
    /// <returns>Auth-Sign header</returns>
    public async Task<string> CreateAuthSignHeader(string action, string authData)
    {
      var signData = $"{authData}{Uri.AbsolutePath}\n{ActionHeader.ToLower()}:{action}\n".ToByteArray();

      return await Task.FromResult(signData.ComputeKeyedHash(Credentials.Key, SignVersion.Algorithm).ToBase64());
    }

    /// <inheritdoc />
    /// <summary>
    /// Generates new Akamai headers and executes the HTTP request
    /// </summary>
    /// <param name="request">Request to be executed</param>
    /// <param name="cancellationToken">Token for cancelling the request</param>
    /// <returns>HTTP response message</returns>
    public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      var headers = await ComputeHeadersAsync();
      foreach (var header in headers)
      {
        request.Headers.Add(header.Key, header.Value);
      }

      return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Computes the required headers for the request
    /// </summary>
    /// <returns>Dictionary containing the headers</returns>
    public async Task<Dictionary<string, string>> ComputeHeadersAsync()
    {
      var action = await CreateActionHeader();
      var authData = await CreateAuthDataHeader();
      var authSign = await CreateAuthSignHeader(action, authData);

      return await Task.FromResult(new Dictionary<string, string>
      {
        {ActionHeader, action},
        {AuthDataHeader, authData},
        {AuthSignHeader, authSign}
      });
    }


    /// <summary>
    /// Executes the request by using Polly to retry failed requests
    /// </summary>
    /// <param name="path">/[CP Code]/[Path]</param>
    /// <param name="method">HTTP method</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> ExecuteWithPollyAsync(string path, HttpMethod method)
    {
      Uri = await GetNetStorageUri(path);

      return await Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => r.IsSuccessStatusCode == false && r.StatusCode != HttpStatusCode.NotFound)
        .WaitAndRetryAsync(5, _ => TimeSpan.FromSeconds(2))
        .ExecuteAsync(() => SendAsync(new HttpRequestMessage(method, Uri), CancellationToken.None));
    }

    /// <summary>
    /// You can "delete" an object from an ObjectStore (NS4) storage group
    /// </summary>
    /// <param name="path">/[CP code]/[path]/[file.ext]</param>
    /// <returns>HTTP response message</returns>
    public new async Task<HttpResponseMessage> DeleteAsync(string path)
    {
      Params = NetStorageAction.Delete;
      return await ExecuteWithPollyAsync(path, HttpMethod.Put);
    }

    /// <summary>
    /// Use the "dir" action with an NS4 storage group to list the objects directly within the specified directory (similar to a standard "ls" or "dir" command)
    /// </summary>
    /// <param name="path">/[CP Code]/[Path]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> DirAsync(string path)
    {
      Params = NetStorageAction.Dir();
      return await ExecuteWithPollyAsync(path, HttpMethod.Get);
    }

    /// <summary>
    /// Use the "download" action to download the specified file from an ObjectStore (NS4) storage group
    /// </summary>
    /// <param name="path">/[CP code]/[path]/[file.ext]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> DownloadAsync(string path)
    {
      Params = NetStorageAction.Download;
      return await ExecuteWithPollyAsync(path, HttpMethod.Get);
    }

    /// <summary>
    /// You can use the "du" action to return disk usage information for a specified directory in an ObjectStore (NS4) storage group
    /// </summary>
    /// <param name="path">/[CP Code]/[Path]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> DUAsync(string path)
    {
      Params = NetStorageAction.DU();
      return await ExecuteWithPollyAsync(path, HttpMethod.Get);
    }

    /// <summary>
    /// You can use the "list" action to recursively list all of the objects within the specified directory.
    /// (This includes all content in all subdirectories that may exist in the named directory's "tree.")
    /// </summary>
    /// <param name="path">/[CP Code]/[Path]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> ListAsync(string path)
    {
      Params = NetStorageAction.List();
      return await ExecuteWithPollyAsync(path, HttpMethod.Get);
    }

    /// <summary>
    /// You can use the "mkdir" action to create a new explicit directory in an ObjectStore (NS4) storage group
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[new_directory]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> MkDirAsync(string path)
    {
      Params = NetStorageAction.MkDir;
      return await ExecuteWithPollyAsync(path, HttpMethod.Put);
    }

    /// <summary>
    /// Incorporate the "mtime" action to change a files modification time ("touch") in an ObjectStore (NS4) storage group
    /// </summary>
    /// <param name="path">/[CP code]/[path]/[file.ext]</param>
    /// <param name="newTime">Set the variable as the desired modification time for the target content (using UNIX epoch time)</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> MTimeAsync(string path, DateTime? newTime = null)
    {
      Params = NetStorageAction.MTime(newTime);
      return await ExecuteWithPollyAsync(path, HttpMethod.Post);
    }

    /// <summary>
    /// You can use the "quick-delete" with ObjectStore (NS4) to perform a delete of a selected directory, including all contents
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[directory]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> QuickDeleteAsync(string path)
    {
      Params = NetStorageAction.QuickDelete;
      return await ExecuteWithPollyAsync(path, HttpMethod.Post);
    }

    /// <summary>
    /// You can use the "rename" action with ObjectStore (NS4) to target a specific file or symbolic link in order to rename it
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[file.ext]</param>
    /// <param name="destination">Include the [CP Code] root, followed by the [path] destination where the renamed file is to reside.
    /// Finally, include the new name [file.ext] for the object and include the extension, if applicable.
    /// Ensure that special characters are query string encoded as required.
    /// For example, any forward slashes ("/") would need to be represented as %2F, in support of query string encoding.</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> RenameAsync(string path, string destination)
    {
      Params = NetStorageAction.Rename(destination);
      return await ExecuteWithPollyAsync(path, HttpMethod.Post);
    }

    /// <summary>
    /// You can delete an empty directory in ObjectStore (NS4) with the "rmdir" action
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[target directory]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> RmDirAsync(string path)
    {
      Params = NetStorageAction.RmDir;
      return await ExecuteWithPollyAsync(path, HttpMethod.Post);
    }

    /// <summary>
    /// You can return stat structure (information) for a named file, symlink or directory with the "stat" action in ObjectStore (NS4)
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[target object]</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> StatAsync(string path)
    {
      Params = NetStorageAction.Stat();
      return await ExecuteWithPollyAsync(path, HttpMethod.Get);
    }

    /// <summary>
    /// You can create a symbolic link in ObjectStore (NS4) with the "symlink" action
    /// Path is the symlink to be created and Target is the existing object in Net Storage
    /// </summary>
    /// <param name="path">/[CP Code]/[path]</param>
    /// <param name="target">Used to define the target of the symlink.
    /// Include the complete [path] to, as well as the name ( [link]) for this file (including the extension, if applicable).
    /// Ensure that special characters (“ /”) are query string encoded. For example, any forward slashes ("/") would need to be represented as %2F, in support of query string encoding.</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> SymLinkAsync(string path, string target)
    {
      Params = NetStorageAction.SymLink(target);
      return await ExecuteWithPollyAsync(path, HttpMethod.Post);
    }

    /// <summary>
    /// You can upload files to an ObjectStore (NS4) storage group with the "upload" action
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[file.ext]</param>
    /// <param name="srcFile">File to be uploaded</param>
    /// <param name="indexZip">Include this to enable az2z processing to index uploaded “.zip” archive files for the “Serve from Zip” feature.
    /// (Archive files must be indexed before they can be used with Serve from Zip.)
    /// The "2" serves as the version currently supported with ObjectStore (NS4) storage groups.</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> UploadAsync(string path, FileInfo srcFile, bool? indexZip = null)
    {
      if (!srcFile.Exists) throw new FileNotFoundException("Src file is not accessible", srcFile.ToString());

      HttpResponseMessage response;
      using (var stream = new BufferedStream(srcFile.OpenRead(), 1024 * 1024))
      {
        var checksum = stream.ComputeHash(HashType.SHA256.Checksum);
        stream.Position = 0;

        Uri = await GetNetStorageUri(path);
        Params = NetStorageAction.Upload(srcFile.LastWriteTime, srcFile.Length, null, null, checksum, indexZip);

        // sanity check to ensure that indexZip is only true if the file destination is also a zip.
        // probably should throw an exception or warning instead.
        if (Params.IndexZip == true && !path.EndsWith(".zip"))
          Params.IndexZip = null;

        // size is not supported with zip since the index-zip funtionality mutates the file thus inconsistency on which size value to use
        // probably should throw an exception or a warning
        if (Params.Size != null && Params.IndexZip == true)
          Params.Size = null;

        response = await Policy
          .Handle<HttpRequestException>()
          .OrResult<HttpResponseMessage>(r => r.IsSuccessStatusCode == false)
          .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(5))
          .ExecuteAsync(() =>
            SendAsync(new HttpRequestMessage(HttpMethod.Put, Uri) {Content = new StreamContent(stream)},
              CancellationToken.None));
      }

      return response;
    }

    /// <summary>
    /// You can upload files to an ObjectStore (NS4) storage group with the "upload" action
    /// </summary>
    /// <param name="path">/[CP Code]/[path]/[file.ext]</param>
    /// <param name="checksum">Computed hash in SHA256 for the source file</param>
    /// <param name="srcFile">Source file as a stream</param>
    /// <param name="lastWriteTime">Last write time of the file</param>
    /// <returns>HTTP response message</returns>
    public async Task<HttpResponseMessage> UploadAsync(string path, byte[] checksum, Stream srcFile,
      DateTime? lastWriteTime = null)
    {
      Uri = await GetNetStorageUri(path);
      Params = NetStorageAction.Upload(lastWriteTime, srcFile.Length, null, null, checksum);

      if (srcFile.Position != 0)
      {
        srcFile.Position = 0;
      }

      return await Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => r.IsSuccessStatusCode == false)
        .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(5))
        .ExecuteAsync(() =>
          SendAsync(new HttpRequestMessage(HttpMethod.Put, Uri) {Content = new StreamContent(srcFile)},
            CancellationToken.None));
    }
  }
}