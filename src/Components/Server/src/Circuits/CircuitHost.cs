// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Browser;
using Microsoft.AspNetCore.Components.Browser.Rendering;
using Microsoft.AspNetCore.Components.Builder;
using Microsoft.AspNetCore.Components.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class CircuitHost : IAsyncDisposable
    {
        private static readonly AsyncLocal<CircuitHost> _current = new AsyncLocal<CircuitHost>();
        private readonly IServiceScope _scope;
        private Action<IComponentsApplicationBuilder> _configure;
        private bool _isInitialized;

        /// <summary>
        /// Gets the current <see cref="Circuit"/>, if any.
        /// </summary>
        public static CircuitHost Current => _current.Value;

        /// <summary>
        /// Sets the current <see cref="Circuits.Circuit"/>.
        /// </summary>
        /// <param name="circuitHost">The <see cref="Circuits.Circuit"/>.</param>
        /// <remarks>
        /// Calling <see cref="SetCurrentCircuitHost(CircuitHost)"/> will store the circuit
        /// and other related values such as the <see cref="IJSRuntime"/> and <see cref="Renderer"/>
        /// in the local execution context. Application code should not need to call this method,
        /// it is primarily used by the Server-Side Components infrastructure.
        /// </remarks>
        public static void SetCurrentCircuitHost(CircuitHost circuitHost)
        {
            _current.Value = circuitHost ?? throw new ArgumentNullException(nameof(circuitHost));

            JSInterop.JSRuntime.SetCurrentJSRuntime(circuitHost.JSRuntime);
            RendererRegistry.SetCurrentRendererRegistry(circuitHost.RendererRegistry);
        }

        public event UnhandledExceptionEventHandler UnhandledException;

        public CircuitHost(
            IServiceScope scope,
            IClientProxy client,
            RendererRegistry rendererRegistry,
            RemoteRenderer renderer,
            Action<IComponentsApplicationBuilder> configure,
            IJSRuntime jsRuntime,
            CircuitSynchronizationContext synchronizationContext,
            Circuit circuit,
            CircuitHandler circuitHandler)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            RendererRegistry = rendererRegistry ?? throw new ArgumentNullException(nameof(rendererRegistry));
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _configure = configure ?? throw new ArgumentNullException(nameof(configure));
            JSRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            SynchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));

            Services = scope.ServiceProvider;

            Circuit = circuit;
            CircuitHandler = circuitHandler;

            Renderer.UnhandledException += Renderer_UnhandledException;
            SynchronizationContext.UnhandledException += SynchronizationContext_UnhandledException;
        }

        public Circuit Circuit { get; }

        public IClientProxy Client { get; }

        public IJSRuntime JSRuntime { get; }

        public RemoteRenderer Renderer { get; }

        public RendererRegistry RendererRegistry { get; }

        public IServiceProvider Services { get; }

        public CircuitSynchronizationContext SynchronizationContext { get; }

        public CircuitHandler CircuitHandler { get; }

        public CancellationToken ConnectionAborted { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await SynchronizationContext.InvokeAsync(async () =>
            {
                SetCurrentCircuitHost(this);

                var builder = new ServerSideComponentsApplicationBuilder(Services);

                _configure(builder);

                for (var i = 0; i < builder.Entries.Count; i++)
                {
                    var entry = builder.Entries[i];
                    Renderer.AddComponent(entry.componentType, entry.domElementSelector);
                }

                await CircuitHandler.OnCircuitOpenedAsync(Circuit, cancellationToken);
                await CircuitHandler.OnConnectionUpAsync(Circuit, cancellationToken);
            });

            _isInitialized = true;
        }

        public async void BeginInvokeDotNetFromJS(string callId, string assemblyName, string methodIdentifier, long dotNetObjectId, string argsJson)
        {
            AssertInitialized();

            try
            {
                await SynchronizationContext.Invoke(() =>
                {
                    SetCurrentCircuitHost(this);

                    DotNetDispatcher.BeginInvoke(callId, assemblyName, methodIdentifier, dotNetObjectId, argsJson);
                });
            }
            catch (Exception ex)
            {
                UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(ex, isTerminating: false));
            }
        }

        public async ValueTask DisposeAsync()
        {
            await SynchronizationContext.InvokeAsync(async () =>
            {
                await CircuitHandler.OnConnectionDownAsync(Circuit, default);
                await CircuitHandler.OnCircuitClosedAsync(Circuit, default);
            });

            _scope.Dispose();
            Renderer.Dispose();
        }

        private void AssertInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Something is calling into the circuit before Initialize() completes");
            }
        }

        private void Renderer_UnhandledException(object sender, Exception e)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(e, isTerminating: false));
        }

        private void SynchronizationContext_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            UnhandledException?.Invoke(this, e);
        }
    }
}
