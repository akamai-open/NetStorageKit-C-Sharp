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

namespace NetStorage.Standard.Models
{
  public static class NetStorageAction
  {
    public static APIParams Delete = new APIParams {Action = "delete"};
    public static APIParams Dir(string format = "xml") => new APIParams {Action = "dir", Format = format};
    public static APIParams Download = new APIParams {Action = "download"};
    public static APIParams DU(string format = "xml") => new APIParams {Action = "du", Format = format};
    public static APIParams List(string format = "xml") => new APIParams {Action = "list", Format = format};
    public static APIParams MkDir = new APIParams {Action = "mkdir"};

    public static APIParams MTime(DateTime? mTime = null) =>
      new APIParams {Action = "mtime", MTime = mTime ?? DateTime.Now};

    public static APIParams QuickDelete = new APIParams {Action = "quick-delete", QuickDelete = "imreallyreallysure"};
    public static APIParams Rename(string destination) => new APIParams {Action = "rename", Destination = destination};
    public static APIParams RmDir = new APIParams {Action = "rmdir"};
    public static APIParams Stat(string format = "xml") => new APIParams {Action = "stat", Format = format};
    public static APIParams SymLink(string target) => new APIParams {Action = "symlink", Target = target};

    public static APIParams Upload(DateTime? mTime = null, long? size = null, byte[] md5Checksum = null,
      byte[] sha1Checksum = null, byte[] sha256Checksum = null, bool? indexZip = null) =>
      new APIParams
      {
        Action = "upload",
        MTime = mTime,
        Size = size,
        MD5 = md5Checksum,
        SHA1 = sha1Checksum,
        SHA256 = sha256Checksum,
        IndexZip = indexZip == true ? indexZip : null
      };
  }
}