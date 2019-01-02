// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;

[assembly: TypeForwardedTo(typeof(MvcLocalizationMvcBuilderExtensions))]
[assembly: TypeForwardedTo(typeof(MvcLocalizationMvcCoreBuilderExtensions))]
[assembly: TypeForwardedTo(typeof(HtmlLocalizer))]
[assembly: TypeForwardedTo(typeof(HtmlLocalizerExtensions))]
[assembly: TypeForwardedTo(typeof(HtmlLocalizerFactory))]
[assembly: TypeForwardedTo(typeof(IHtmlLocalizer))]
[assembly: TypeForwardedTo(typeof(IHtmlLocalizerFactory))]
[assembly: TypeForwardedTo(typeof(IViewLocalizer))]
[assembly: TypeForwardedTo(typeof(LocalizedHtmlString))]
[assembly: TypeForwardedTo(typeof(ViewLocalizer))]