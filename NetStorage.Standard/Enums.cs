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

namespace NetStorage.Standard
{
  /// <summary>
  /// An enum of the hash algorithms supported by <see cref="#ExtensionMethods.ComputeKeyedHash(Stream, KeyedHashAlgorithm"/>
  /// Currently supported hashes include MD5; SHA1; SHA256
  ///
  /// The string representation matches the <see cref="System.Security.Cryptography.HMAC"/> canonical names.
  /// </summary>
  public enum KeyedHashAlgorithm
  {
    HMACSHA256,
    HMACSHA1,
    HMACMD5
  }

  /// <summary>
  /// An enum of the hash algorithms supported by <see cref="#ExtensionMethods.ComputeHash(Stream, ChecksumAlgorithm"/>
  /// Currently supported hashes include MD5; SHA1; SHA256
  ///
  /// The string representation matches the <see cref="System.Security.Cryptography.HashAlgorithm"/> canonical names.
  /// </summary>
  public enum ChecksumAlgorithm
  {
    SHA256,
    SHA1,
    MD5
  }
}