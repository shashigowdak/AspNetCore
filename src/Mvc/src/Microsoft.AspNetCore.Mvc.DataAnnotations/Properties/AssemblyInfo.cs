// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;

[assembly: TypeForwardedTo(typeof(AttributeAdapterBase<>))]
[assembly: TypeForwardedTo(typeof(RequiredAttributeAdapter))]
[assembly: TypeForwardedTo(typeof(MvcDataAnnotationsLocalizationOptions))]
[assembly: TypeForwardedTo(typeof(MvcDataAnnotationsMvcBuilderExtensions))]
[assembly: TypeForwardedTo(typeof(MvcDataAnnotationsMvcCoreBuilderExtensions))]

