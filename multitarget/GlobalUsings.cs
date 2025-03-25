// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using System;
global using System.Collections.Generic;

#if NETFRAMEWORK
global using Microsoft.IO;
global using Microsoft.IO.Enumeration;
#else
global using System.IO;
global using System.IO.Enumeration;
#endif

// This isn't defined in Microsoft.IO to facilitate value exchange.
global using FileAttributes = System.IO.FileAttributes;
