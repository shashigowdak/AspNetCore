// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

[assembly: TypeForwardedTo(typeof(IApiDescriptionProvider))]
[assembly: TypeForwardedTo(typeof(ApiDescription))]
[assembly: TypeForwardedTo(typeof(ApiDescriptionProviderContext))]
[assembly: TypeForwardedTo(typeof(ApiParameterDescription))]
[assembly: TypeForwardedTo(typeof(ApiParameterRouteInfo))]
[assembly: TypeForwardedTo(typeof(ApiRequestFormat))]
[assembly: TypeForwardedTo(typeof(ApiResponseFormat))]
[assembly: TypeForwardedTo(typeof(ApiResponseType))]

[assembly: TypeForwardedTo(typeof(ApiDescriptionExtensions))]
[assembly: TypeForwardedTo(typeof(ApiDescriptionGroup))]
[assembly: TypeForwardedTo(typeof(ApiDescriptionGroupCollection))]
[assembly: TypeForwardedTo(typeof(ApiDescriptionGroupCollectionProvider))]
[assembly: TypeForwardedTo(typeof(IApiDescriptionGroupCollectionProvider))]
