// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http
{
    public class Http1OutputProducer : IHttpOutputProducer, IHttpOutputAborter, IDisposable
    {
        private static readonly ReadOnlyMemory<byte> _continueBytes = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"));
        private static readonly byte[] _bytesHttpVersion11 = Encoding.ASCII.GetBytes("HTTP/1.1 ");
        private static readonly byte[] _bytesEndHeaders = Encoding.ASCII.GetBytes("\r\n\r\n");
        private static readonly ReadOnlyMemory<byte> _endChunkedResponseBytes = new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("0\r\n\r\n"));

        private readonly string _connectionId;
        private readonly ConnectionContext _connectionContext;
        private readonly IKestrelTrace _log;
        private readonly IHttpMinResponseDataRateFeature _minResponseDataRateFeature;
        private readonly TimingPipeFlusher _flusher;

        // This locks access to to all of the below fields
        private readonly object _contextLock = new object();

        private bool _completed = false;
        private bool _aborted;
        private long _unflushedBytes;

        private readonly PipeWriter _pipeWriter;

        // internal memory abstraction
        private List<CompletedBuffer> _completedSegments;
        private Memory<byte> _currentSegment;
        private IMemoryOwner<byte> _currentSegmentOwner;
        private MemoryPool<byte> _pool;
        private int _position;
        private readonly int _minimumSegmentSize = 4096;
        private bool _hasWrittenResponseHeader;
        private bool _autoChunk;

        public Http1OutputProducer(
            PipeWriter pipeWriter,
            string connectionId,
            ConnectionContext connectionContext,
            IKestrelTrace log,
            ITimeoutControl timeoutControl,
            IHttpMinResponseDataRateFeature minResponseDataRateFeature,
            MemoryPool<byte> pool)
        {
            _pipeWriter = pipeWriter;
            _connectionId = connectionId;
            _connectionContext = connectionContext;
            _log = log;
            _minResponseDataRateFeature = minResponseDataRateFeature;
            _flusher = new TimingPipeFlusher(pipeWriter, timeoutControl, log);
            _pool = pool;
        }

        public Task WriteDataAsync(ReadOnlySpan<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return WriteAsync(buffer, cancellationToken).AsTask();
        }

        public ValueTask<FlushResult> WriteDataToPipeAsync(ReadOnlySpan<byte> buffer, CancellationToken cancellationToken = default)
        {
            // TODO locking
            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<FlushResult>(Task.FromCanceled<FlushResult>(cancellationToken));
            }

            return WriteAsync(buffer, cancellationToken);
        }

        public ValueTask<FlushResult> WriteStreamSuffixAsync()
        {
            return WriteAsync(_endChunkedResponseBytes.Span);
        }

        public ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            return WriteAsync(Constants.EmptyData, cancellationToken);
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (!_hasWrittenResponseHeader)
            {
                if (_autoChunk)
                {
                    // 10 characters for chunked header and 2 for \r\n
                    EnsureCapacity(sizeHint + 12);
                }
                else
                {
                    EnsureCapacity(sizeHint);
                }

                return _currentSegment.Slice(_position);
            }

            return _pipeWriter.GetMemory(sizeHint);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (!_hasWrittenResponseHeader)
            {
                if (_autoChunk)
                {
                    // 10 characters for chunked header and 2 for \r\n
                    EnsureCapacity(sizeHint + 12);
                }
                else
                {
                    EnsureCapacity(sizeHint);
                }
                return _currentSegment.Span.Slice(_position);

            }
            return _pipeWriter.GetSpan(sizeHint);
        }

        public void Advance(int bytes)
        {
            if (!_hasWrittenResponseHeader)
            {
                if (_currentSegment.IsEmpty) // TODO confirm this
                {
                    throw new InvalidOperationException("No writing operation. Make sure GetMemory() was called.");
                }

                if (bytes >= 0)
                {
                    if (_currentSegment.Length < _position + bytes)
                    {
                        throw new InvalidOperationException("Can't advance past buffer size.");
                    }
                    _position += bytes;
                }
            }
            else
            {
                _pipeWriter.Advance(bytes);
            }
        }

        public void CancelPendingFlush()
        {
            _pipeWriter.CancelPendingFlush();
        }

        // This method is for chunked http responses
        public ValueTask<FlushResult> WriteAsync<T>(Func<PipeWriter, T, long> callback, T state, CancellationToken cancellationToken)
        {
            lock (_contextLock)
            {
                if (_completed)
                {
                    return default;
                }

                var buffer = _pipeWriter;
                var bytesCommitted = callback(buffer, state);
                _unflushedBytes += bytesCommitted;
            }

            return FlushAsync(cancellationToken);
        }

        public void WriteResponseHeaders(int statusCode, string reasonPhrase, HttpResponseHeaders responseHeaders, bool autoChunk)
        {
            lock (_contextLock)
            {
                if (_completed)
                {
                    return;
                }

                var buffer = _pipeWriter;
                var writer = new BufferWriter<PipeWriter>(buffer);

                writer.Write(_bytesHttpVersion11);
                var statusBytes = ReasonPhrases.ToStatusBytes(statusCode, reasonPhrase);
                writer.Write(statusBytes);
                responseHeaders.CopyTo(ref writer);
                writer.Write(_bytesEndHeaders);

                writer.Commit();

                _unflushedBytes += writer.BytesCommitted;
                _hasWrittenResponseHeader = true;
                _autoChunk = autoChunk;

                // TODO this can be simplified, I'm just trying to see if this works
                if (_completedSegments != null)
                {
                    for (var i = 0; i < _completedSegments.Count; i++)
                    {
                        var segment = _completedSegments[i];
                        if (autoChunk)
                        {
                            if (segment.Length > 0)
                            {
                                var bufferWriter = new BufferWriter<PipeWriter>(_pipeWriter);
                                bufferWriter.WriteBeginChunkBytes(segment.Length);
                                bufferWriter.Write(segment.Buffer.Span.Slice(segment.Length)); // TODO the size may be incorrect here.
                                bufferWriter.WriteEndChunkBytes();
                                writer.Commit();
                                _unflushedBytes += bufferWriter.BytesCommitted;
                            }
                            segment.Return();
                        }
                        else
                        {
                            var memory = _pipeWriter.GetMemory(segment.Length);
                            segment.Buffer.CopyTo(memory);
                            segment.Return();
                        }
                    }

                    _completedSegments.Clear();
                }

                if (!_currentSegment.IsEmpty)
                {
                    if (autoChunk)
                    {
                        if (_position > 0)
                        {
                            var bufferWriter = new BufferWriter<PipeWriter>(_pipeWriter);
                            bufferWriter.WriteBeginChunkBytes(_position);
                            bufferWriter.Write(_currentSegment.Span.Slice(0, _position)); // TODO the size may be incorrect here.
                            bufferWriter.WriteEndChunkBytes();
                            bufferWriter.Commit();
                            _unflushedBytes += bufferWriter.BytesCommitted;
                        }
                    }
                    else
                    {
                        var memory = _pipeWriter.GetMemory(_currentSegment.Length);
                        _currentSegment.Slice(0, _position).CopyTo(memory);
                        _pipeWriter.Advance(_position);
                    }

                }
            }
        }

        public void Dispose()
        {
            lock (_contextLock)
            {
                if (_completed)
                {
                    return;
                }

                _log.ConnectionDisconnect(_connectionId);
                _completed = true;
                _pipeWriter.Complete();

                if (_completedSegments != null)
                {
                    foreach (var segment in _completedSegments)
                    {
                        segment.Return();
                    }
                }

                _currentSegmentOwner?.Dispose();
            }
        }

        public void Abort(ConnectionAbortedException error)
        {
            // Abort can be called after Dispose if there's a flush timeout.
            // It's important to still call _lifetimeFeature.Abort() in this case.

            lock (_contextLock)
            {
                if (_aborted)
                {
                    return;
                }

                _aborted = true;
                _connectionContext.Abort(error);
                Dispose();
            }
        }

        public ValueTask<FlushResult>  Write100ContinueAsync()
        {
            return WriteAsync(_continueBytes.Span);
        }

        private ValueTask<FlushResult> WriteAsync(
            ReadOnlySpan<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            lock (_contextLock)
            {
                if (_completed)
                {
                    return default;
                }

                var writer = new BufferWriter<PipeWriter>(_pipeWriter);
                if (buffer.Length > 0)
                {
                    writer.Write(buffer);

                    _unflushedBytes += buffer.Length;
                }
                writer.Commit();

                var bytesWritten = _unflushedBytes;
                _unflushedBytes = 0;

                return _flusher.FlushAsync(
                    _minResponseDataRateFeature.MinDataRate,
                    bytesWritten,
                    this,
                    cancellationToken);
            }
        }

        private void EnsureCapacity(int sizeHint)
        {
            // This does the Right Thing. It only subtracts _position from the current segment length if it's non-null.
            // If _currentSegment is null, it returns 0.
            var remainingSize = _currentSegment.Length - _position;

            // If the sizeHint is 0, any capacity will do
            // Otherwise, the buffer must have enough space for the entire size hint, or we need to add a segment.
            if ((sizeHint == 0 && remainingSize > 0) || (sizeHint > 0 && remainingSize >= sizeHint))
            {
                // We have capacity in the current segment
                return;
            }

            AddSegment(sizeHint);
        }

        private void AddSegment(int sizeHint = 0)
        {
            if (_currentSegment.Length != 0)
            {
                // We're adding a segment to the list
                if (_completedSegments == null)
                {
                    _completedSegments = new List<CompletedBuffer>();
                }

                // Position might be less than the segment length if there wasn't enough space to satisfy the sizeHint when
                // GetMemory was called. In that case we'll take the current segment and call it "completed", but need to
                // ignore any empty space in it.
                _completedSegments.Add(new CompletedBuffer(_currentSegmentOwner, _position));
            }

            // Get a new buffer using the minimum segment size, unless the size hint is larger than a single segment.
            _currentSegmentOwner = _pool.Rent(Math.Max(_minimumSegmentSize, sizeHint));
            _currentSegment = _currentSegmentOwner.Memory;
            _position = 0;
        }

        /// <summary>
        /// Holds a byte[] from the pool and a size value. Basically a Memory but guaranteed to be backed by an ArrayPool byte[], so that we know we can return it.
        /// </summary>
        private readonly struct CompletedBuffer
        {
            public Memory<byte> Buffer { get; }
            public int Length { get; }

            public ReadOnlySpan<byte> Span => Buffer.Span;

            private readonly IMemoryOwner<byte> _memoryOwner;

            public CompletedBuffer(IMemoryOwner<byte> buffer, int length)
            {
                Buffer = buffer.Memory;
                Length = length;
                _memoryOwner = buffer;
            }

            public void Return()
            {
                _memoryOwner.Dispose();
            }
        }
    }
}
