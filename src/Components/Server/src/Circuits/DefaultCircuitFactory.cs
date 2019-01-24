// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Components.Browser;
using Microsoft.AspNetCore.Components.Browser.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class DefaultCircuitFactory : CircuitFactory
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ComponentsServerOptions _options;

        public DefaultCircuitFactory(
            IServiceScopeFactory scopeFactory,
            IOptions<ComponentsServerOptions> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options.Value;
        }

        public override CircuitHost CreateCircuitHost(HttpContext httpContext, IClientProxy client)
        {
            if (!_options.StartupActions.TryGetValue(httpContext.Request.Path, out var config))
            {
                var message = $"Could not find an ASP.NET Core Components startup action for request path '{httpContext.Request.Path}'.";
                throw new InvalidOperationException(message);
            }

            var circuit = new Circuit(Guid.NewGuid().ToString());
            var scope = _scopeFactory.CreateScope();
            var jsRuntime = new RemoteJSRuntime(client);
            var rendererRegistry = new RendererRegistry();
            var synchronizationContext = new CircuitSynchronizationContext();
            var renderer = new RemoteRenderer(scope.ServiceProvider, rendererRegistry, jsRuntime, client, synchronizationContext);

            var circuitHandler = GetCircuitHandler(httpContext, scope);

            var circuitHost = new CircuitHost(
                scope,
                client,
                rendererRegistry,
                renderer,
                config,
                jsRuntime,
                synchronizationContext,
                circuit,
                circuitHandler);

            // Initialize per-circuit data that services need
            (circuitHost.Services.GetRequiredService<IJSRuntimeAccessor>() as DefaultJSRuntimeAccessor).JSRuntime = jsRuntime;
            (circuitHost.Services.GetRequiredService<ICircuitAccessor>() as DefaultCircuitAccessor).Circuit = circuitHost.Circuit;

            return circuitHost;
        }

        private CircuitHandler GetCircuitHandler(HttpContext httpContext, IServiceScope scope)
        {
            // Is there a specific handler for this component hub?
            if (!_options.CircuitHandlers.TryGetValue(httpContext.Request.Path, out var handlerType))
            {
                // Nope, perhaps there's a default one specified.
                handlerType = _options.DefaultCircuitHandler;
            }

            var circuitHandler = CircuitHandler.NullHandler;
            if (handlerType != null)
            {
                circuitHandler = (CircuitHandler)scope.ServiceProvider.GetRequiredService(handlerType);
            }

            return circuitHandler;
        }
    }
}
