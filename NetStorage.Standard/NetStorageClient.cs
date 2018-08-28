﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetStorage.Standard.Models;

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

    public async Task<Uri> GetNetStorageUri(string path)
    {
      return await Task.FromResult(
        new UriBuilder {Scheme = Credentials.UseSSL ? "HTTPS" : "HTTP", Host = Credentials.HostName, Path = path}
          .Uri);
    }

    public async Task<string> CreateActionHeader()
    {
      return await Task.FromResult(Params.ConvertToQueryString(Signer.ParamsNameFormatter,
        Signer.ParamsValueFormatter));
    }

    public async Task<string> CreateAuthDataHeader()
    {
      return await Task.FromResult(
        $"{SignVersion.VersionID}, 0.0.0.0, 0.0.0.0, {DateTime.UtcNow.GetEpochSeconds()}, {new Random().Next()}, {Credentials.Username}");
    }

    public async Task<string> CreateAuthSignHeader(string action, string authData)
    {
      var signData = $"{authData}{Uri.AbsolutePath}\n{ActionHeader.ToLower()}:{action}\n".ToByteArray();

      return await Task.FromResult(signData.ComputeKeyedHash(Credentials.Key, SignVersion.Algorithm).ToBase64());
    }

    public async Task<XDocument> DirAsync(string path)
    {
      Uri = await GetNetStorageUri(path);
      Params = NetStorageAction.Dir();
      var request = new HttpRequestMessage(HttpMethod.Get, Uri);
      var response = await SendAsync(request, CancellationToken.None);
      if (response.IsSuccessStatusCode)
      {
        var data = await response.Content.ReadAsStringAsync();
        return XDocument.Parse(data);
      }

      return null;
    }

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
  }
}