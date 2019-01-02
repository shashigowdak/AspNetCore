// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Cors;
using Microsoft.Extensions.DependencyInjection;

[assembly: TypeForwardedTo(typeof(CorsAuthorizationFilter))]
[assembly: TypeForwardedTo(typeof(MvcCoreMvcBuilderExtensions))]
