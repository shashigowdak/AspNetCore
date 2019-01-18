// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using System.IO.Pipelines;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http
{
    // make this wrap a HttpResponsePipe which calls directly into OutputProducer
    // Let's just start with a type for PipeWriter'
    internal class HttpResponseStream : WriteOnlyPipeStream
    {
        private readonly HttpResponsePipeWriter _pipeWriter;

        public HttpResponseStream(HttpResponsePipeWriter pipeWriter)
            : base(pipeWriter)
        {
            _pipeWriter = pipeWriter;
        }

        public void StartAcceptingWrites()
        {
            _pipeWriter.StartAcceptingWrites();
        }

        public void StopAcceptingWrites()
        {
            _pipeWriter.StopAcceptingWrites();
        }

        public void Abort()
        {
            _pipeWriter.Abort();
        }
    }
}
