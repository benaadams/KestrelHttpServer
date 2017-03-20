// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Internal.Http;

namespace Microsoft.AspNetCore.Server.Kestrel.Adapter.Internal
{
    public class StreamSocketOutput : ISocketOutput
    {
        private static readonly ArraySegment<byte> _nullBuffer = new ArraySegment<byte>(new byte[0]);

        private readonly Stream _outputStream;
        private readonly IPipe _pipe;
        private object _sync = new object();
        private bool _completed;

        private PipelineWriter _writer = new PipelineWriter();

        public StreamSocketOutput(Stream outputStream, IPipe pipe)
        {
            _outputStream = outputStream;
            _pipe = pipe;
        }

        public void Write(ArraySegment<byte> buffer, bool chunk)
        {
            WriteAsync(buffer, chunk, default(CancellationToken)).GetAwaiter().GetResult();
        }

        public async Task WriteAsync(ArraySegment<byte> buffer, bool chunk, CancellationToken cancellationToken)
        {
            var flushAwaiter = default(WritableBufferAwaitable);

            lock (_sync)
            {
                if (_completed)
                {
                    return;
                }

                _writer.Initalize(_pipe.Writer.Alloc());

                if (buffer.Count > 0)
                {
                    if (chunk)
                    {
                        ChunkWriter.WriteBeginChunkBytes(_writer, buffer.Count);
                        _writer.WriteFast(buffer);
                        ChunkWriter.WriteEndChunkBytes(_writer);
                    }
                    else
                    {
                        _writer.WriteFast(buffer);
                    }
                }

                flushAwaiter = _writer.FlushAsync();
            }

            await flushAwaiter;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _completed = true;
            }

            _pipe.Writer.Complete();
        }

        public void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            return WriteAsync(default(ArraySegment<byte>), chunk: false, cancellationToken: cancellationToken);
        }

        public WritableBuffer Alloc()
        {
            return _pipe.Writer.Alloc();
        }

        public async Task WriteOutputAsync()
        {
            try
            {
                while (true)
                {
                    var readResult = await _pipe.Reader.ReadAsync();
                    var buffer = readResult.Buffer;

                    try
                    {
                        if (buffer.IsEmpty && readResult.IsCompleted)
                        {
                            break;
                        }

                        if (buffer.IsEmpty)
                        {
                            await _outputStream.FlushAsync();
                        }

                        if (buffer.IsSingleSpan)
                        {
                            var array = buffer.First.GetArray();
                            await _outputStream.WriteAsync(array.Array, array.Offset, array.Count);
                        }
                        else
                        {
                            foreach (var memory in buffer)
                            {
                                var array = memory.GetArray();
                                await _outputStream.WriteAsync(array.Array, array.Offset, array.Count);
                            }
                        }
                    }
                    finally
                    {
                        _pipe.Reader.Advance(readResult.Buffer.End);
                    }
                }
            }
            finally
            {
                _pipe.Reader.Complete();
            }
        }
    }
}
