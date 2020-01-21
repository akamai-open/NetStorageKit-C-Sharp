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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NetStorage.Standard.Test
{
  public class FooHandler : HttpMessageHandler
  {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      var content = await GetContent(request).ConfigureAwait(false);
      return await Task.FromResult(new HttpResponseMessage
      {
        StatusCode = HttpStatusCode.OK,
        Content = new ReadOnlyMemoryContent(new ReadOnlyMemory<byte>(content.ToByteArray()))
      });
    }

    private static async Task<string> GetContent(HttpRequestMessage request)
    {
      var action = request.RequestUri.Segments[1];
      switch (action)
      {
        case "dir":
          return await GetDirContent().ConfigureAwait(false);
        case "delete":
          return "";
        case "download":
          return "";
        case "du":
          return await GetDUContent().ConfigureAwait(false);
        case "list":
          return await GetListContent().ConfigureAwait(false);
        case "mkdir":
          return "";
        case "mtime":
          return "";
        case "quick-delete":
          return "";
        case "rename":
          return "";
        case "rmdir":
          return "";
        case "stat":
          return await GetStatContent().ConfigureAwait(false);
        case "symlink":
          return "";
        case "upload":
          return "";
        default:
          return null;
      }
    }

    private static async Task<string> GetDirContent()
    {
      const string content = @"
        <stat directory=""/[CP code]/sampledir"">
            <file type=""file"" name=""File2"" size=""398421"" md5=""[HASH]"" mtime=""1524068379""/>
            <file type=""symlink"" name=""My_symlink.html"" target=""File1"" mtime=""1524110333""/>
            <file type=""dir"" name=""dir1"" bytes=""19873716"" files=""6"" mtime=""1524068415"" implicit=""true""/>
            <file type=""dir"" name=""dir2"" bytes=""3874912"" files=""1"" mtime=""1524068422"" implicit=""true""/>
            <file type=""dir"" name=""explicitdir1"" bytes=""0"" files=""1"" mtime=""1524068459""/>
            <file type=""dir"" name=""explicitdir2"" bytes=""3"" files=""2"" mtime=""1524068462""/>
            <file type=""file"" name=""file1"" size=""532459"" md5=""[HASH]"" mtime=""1524068382""/>
        </stat>";

      return await Task.FromResult(content);
    }

    private static async Task<string> GetDUContent()
    {
      const string content = @"
        <du directory=""/[CP Code]/dir1/dir2""> 
            <du-info files=""12399999"" bytes=""383838383838""> 
        </du>";

      return await Task.FromResult(content);
    }

    private static async Task<string> GetListContent()
    {
      const string content = @"
        <list>
            <file type=""file"" name=""[CP Code]/File1.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068379""/>
            <file type=""file"" name=""[CP Code]/File2.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068382""/>
            <file type=""file"" name=""[CP Code]/implicit1/File3.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068395""/>
            <file type=""file"" name=""[CP Code]/implicit1/File4.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068407""/>
            <file type=""file"" name=""[CP Code]/implicit1/implicit2/File5.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068405""/>
            <file type=""file"" name=""[CP Code]/implicit1/implicit2/File6.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068410""/>
            <file type=""dir"" name=""[CP Code]/explicitdir1/""/>
            <file type=""dir"" name=""[CP Code]/explicitdir2/""/>
            <file type=""file"" name=""[CP Code]/explicitdir2/File10.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068475""/>
            <file type=""file"" name=""[CP Code]/explicitdir2/implicit/File9.ext"" size=""3"" md5=""[HASH]"" mtime=""1524068475""/>
            <file type=""symlink"" name=""[CP Code]/explicitdir2/link1""/>
        </list>";

      return await Task.FromResult(content);
    }

    private static async Task<string> GetStatContent()
    {
      const string content = @"
        <stat directory=""/dir1/dir2""> 
            <file type=""file"" name=""file.html"" mtime=""1260000000"" size=""1234567"" md5=""0123456789abcdef0123456789abcdef"" /> 
        </stat>";
      return await Task.FromResult(content);
    }
  }
}