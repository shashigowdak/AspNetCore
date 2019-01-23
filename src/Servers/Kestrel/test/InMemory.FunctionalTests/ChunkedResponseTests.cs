// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Server.Kestrel.Tests;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests
{
    public class ChunkedResponseTests : LoggedTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ResponsesAreChunkedAutomatically(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("Hello "), 0, 6, isPipeTest);
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("World!"), 0, 6, isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host:",
                        "",
                        "");
                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "6",
                        "Hello ",
                        "6",
                        "World!",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ResponsesAreNotChunkedAutomaticallyForHttp10Requests(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                await httpContext.Response.WriteResponseAsync("Hello ", isPipeTest);
                await httpContext.Response.WriteResponseAsync("World!", isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.0",
                        "Connection: keep-alive",
                        "",
                        "");
                    await connection.ReceiveEnd(
                        "HTTP/1.1 200 OK",
                        "Connection: close",
                        $"Date: {testContext.DateHeaderValue}",
                        "",
                        "Hello World!");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ResponsesAreChunkedAutomaticallyForHttp11NonKeepAliveRequests(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                await httpContext.Response.WriteResponseAsync("Hello ", isPipeTest);
                await httpContext.Response.WriteResponseAsync("World!", isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "Connection: close",
                        "",
                        "");
                    await connection.ReceiveEnd(
                        "HTTP/1.1 200 OK",
                        "Connection: close",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "6",
                        "Hello ",
                        "6",
                        "World!",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SettingConnectionCloseHeaderInAppDoesNotDisableChunking(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                httpContext.Response.Headers["Connection"] = "close";
                await httpContext.Response.WriteResponseAsync("Hello ", isPipeTest);
                await httpContext.Response.WriteResponseAsync("World!", isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.ReceiveEnd(
                        "HTTP/1.1 200 OK",
                        "Connection: close",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "6",
                        "Hello ",
                        "6",
                        "World!",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ZeroLengthWritesAreIgnored(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("Hello "), 0, 6, isPipeTest);
                await response.WriteResponseBodyAsync(new byte[0], 0, 0, isPipeTest);
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("World!"), 0, 6, isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "6",
                        "Hello ",
                        "6",
                        "World!",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ZeroLengthWritesFlushHeaders(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            var flushed = new SemaphoreSlim(0, 1);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseAsync("", isPipeTest);

                await flushed.WaitAsync();

                await response.WriteResponseAsync("Hello World!", isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");

                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "");

                    flushed.Release();

                    await connection.Receive(
                        "c",
                        "Hello World!",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EmptyResponseBodyHandledCorrectlyWithZeroLengthWrite(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseBodyAsync(new byte[0], 0, 0, isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConnectionClosedIfExceptionThrownAfterWrite(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("Hello World!"), 0, 12, isPipeTest);
                throw new Exception();
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    // SendEnd is not called, so it isn't the client closing the connection.
                    // client closing the connection.
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.ReceiveEnd(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "c",
                        "Hello World!",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConnectionClosedIfExceptionThrownAfterZeroLengthWrite(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseBodyAsync(new byte[0], 0, 0, isPipeTest);
                throw new Exception();
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    // SendEnd is not called, so it isn't the client closing the connection.
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");

                    // Headers are sent before connection is closed, but chunked body terminator isn't sent
                    await connection.ReceiveEnd(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WritesAreFlushedPriorToResponseCompletion(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            var flushWh = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("Hello "), 0, 6, isPipeTest);

                // Don't complete response until client has received the first chunk.
                await flushWh.Task.DefaultTimeout();

                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("World!"), 0, 6, isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "6",
                        "Hello ",
                        "");

                    flushWh.SetResult(null);

                    await connection.Receive(
                        "6",
                        "World!",
                        "0",
                        "",
                        "");
                }
                await server.StopAsync();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ChunksCanBeWrittenManually(bool isPipeTest)
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;
                response.Headers["Transfer-Encoding"] = "chunked";

                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("6\r\nHello \r\n"), 0, 11, isPipeTest);
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("6\r\nWorld!\r\n"), 0, 11, isPipeTest);
                await response.WriteResponseBodyAsync(Encoding.ASCII.GetBytes("0\r\n\r\n"), 0, 5, isPipeTest);
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "6",
                        "Hello ",
                        "6",
                        "World!",
                        "0",
                        "",
                        "");
                }

                await server.StopAsync();
            }
        }

        [Fact]
        public async Task ChunksWithGetMemoryBeforeFirstFlushStillFlushes()
        {
            var testContext = new TestServiceContext(LoggerFactory);

            using (var server = new TestServer(async httpContext =>
            {
                var response = httpContext.Response;

                var memory = response.BodyPipe.GetMemory();
                var fisrtPartOfResponse = Encoding.ASCII.GetBytes("Hello ");
                fisrtPartOfResponse.CopyTo(memory);
                response.BodyPipe.Advance(6);

                memory = response.BodyPipe.GetMemory();
                var secondPartOfResponse = Encoding.ASCII.GetBytes("World!");
                secondPartOfResponse.CopyTo(memory);
                response.BodyPipe.Advance(6);

                await response.BodyPipe.FlushAsync();
            }, testContext))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(
                        "GET / HTTP/1.1",
                        "Host: ",
                        "",
                        "");
                    await connection.Receive(
                        "HTTP/1.1 200 OK",
                        $"Date: {testContext.DateHeaderValue}",
                        "Transfer-Encoding: chunked",
                        "",
                        "c",
                        "Hello World!",
                        "0",
                        "",
                        "");
                }

                await server.StopAsync();
            }
        }
    }
}

