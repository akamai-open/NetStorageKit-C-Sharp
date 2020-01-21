# NetStorageKit (for .NET/c#)

This library assists in the interaction with Akamai's NetStorage CMS API. The CMS API Spec can be found at:
https://control.akamai.com/dl/customers/NS/NS_http_api_OS.pdf

## Project organization
* `/NetStorage` - core NetStorage project
* `/NetStorageTest` - MSTest unit tests
* `/NetStorageExample` - example CMS.exe implementation
* `/NetStorageKit.sln` - root VisualStudio solution

## Install
* Open the NetStorageKit.sln in Visual Studio; Rebuild All
* OR ```MSBuild.exe NetStorageKit.sln /t:rebuild```
* Copy the Akamai.Netstorage.dll to your application or solution. (`/NetStorage/obj/Debug/Akamai.Netstorage.dll` or `/NetStorage/obj/Release/Akamai.Netstorage.dll`)

## Getting Started
* Create an instance of the `NetStorage` object by passing in the host, username and key
* Issue a command to NetStorage by calling the appropriate method from the `NetStorage` object

For example, to delete a file:
```c#
using Akamai.NetStorage;

NetStorage ns = new NetStorage("example.akamaihd.net", "user1", "1234abcd");
ns.Delete("/1234/example.zip");
```

Other methods return a `Stream`. For example, to retrieve a directory listing:

```c#
using Akamai.NetStorage;
NetStorage ns = new NetStorage("example.akamaihd.net", "user1", "1234abcd");

try (Stream result = ns.Dir("/1234")) {
 // TODO: consume Stream
}
```

Finally, when uploading a `FileInfo` object can be sent or an open `InputStream` wll be used
```c#
using Akamai.NetStorage;

NetStorage ns = new NetStorage("example.akamaihd.net", "user1", "1234abcd");
try (bool success = ns.Upload("/1234/example.zip", new FileInfo("../workingdir/srcfile.zip"))) {
 // TODO: log support
}
```


## Sample application (CMS)
* A sample application has been created that can take command line parameters.

`CMS.exe -a dir -u user1 -k 1234abcd example.akamaihd.net/1234`


# NetStorageKit .NET Standard

This updated version of the NetStorageKit is built with .NET Standard and uses HtttpClient to send asynchronous requests to the Net Storage API.

This package is available to download from the global nuget feed:
https://www.nuget.org/packages/NetStorageKit.Standard/

For example, to retrieve the directory contents:
```c#
using NetStorage.Standard;
using NetStorage.Standard.Models;

  using (var client = new NetStorageClient(new NetStorageCredentials("example.akamaihd.net", "user1", "1234abcd")))
  {
	var response = await client.DirAsync("/1234");
	// TODO: read the HttpResponseMessage content
  }
```

