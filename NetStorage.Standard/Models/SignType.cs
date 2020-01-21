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

// ReSharper disable InconsistentNaming

namespace NetStorage.Standard.Models
{
  public class SignType
  {
    public static SignType HMACMD5 = new SignType(3, KeyedHashAlgorithm.HMACMD5);
    public static SignType HMACSHA1 = new SignType(4, KeyedHashAlgorithm.HMACSHA1);
    public static SignType HMACSHA256 = new SignType(5, KeyedHashAlgorithm.HMACSHA256);

    public int VersionID { get; }
    public KeyedHashAlgorithm Algorithm { get; }

    private SignType(int versionID, KeyedHashAlgorithm algorithm)
    {
      VersionID = versionID;
      Algorithm = algorithm;
    }
  }
}