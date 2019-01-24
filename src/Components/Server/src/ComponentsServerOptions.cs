// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components.Builder;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Components.Server
{
    public class ComponentsServerOptions
    {
        private readonly Dictionary<PathString, Type> _circuitHandlers = new Dictionary<PathString, Type>();

        public IReadOnlyDictionary<PathString, Type> CircuitHandlers => _circuitHandlers;

        public Type DefaultCircuitHandler { get; private set; }

        public void AddCircuitHandler<THandler>(PathString path) where THandler : CircuitHandler
        {
            if (!path.HasValue)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _circuitHandlers.Add(path, typeof(THandler));
        }

        public void SetDefaultHandler<THandler>() where THandler : CircuitHandler
        {
            DefaultCircuitHandler = typeof(THandler);
        }

        // During the DI configuration phase, we use Configure<DefaultCircuitFactoryOptions>(...)
        // callbacks to build up this dictionary mapping paths to startup actions
        internal Dictionary<PathString, Action<IComponentsApplicationBuilder>> StartupActions { get; }
            = new Dictionary<PathString, Action<IComponentsApplicationBuilder>>();
    }
}
