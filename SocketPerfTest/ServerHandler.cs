using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace SslStreamPerf
{
    // TODO: Rename to connection handler ,and add flag for isclient or something like that

    internal sealed class ConnectionHandler
    {
        private readonly Stream _stream;
        private readonly ConnectionManager _connectionManager;
        private readonly bool _isClient;

        private readonly Memory<byte> _readBuffer;
        private readonly ReadOnlyMemory<byte> _writeBuffer;

        private int _requestCount;

        public ConnectionHandler(ConnectionManager connectionManager, Stream stream, bool isClient)
        {
            _connectionManager = connectionManager;
            _stream = stream;
            _isClient = isClient;

            _readBuffer = new Memory<byte>(new byte[4096]);

            _writeBuffer = ConstructMessage(_connectionManager.MessageSize);
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        private static ReadOnlyMemory<byte> ConstructMessage(int messageSize)
        {
            // Create zero-terminated message of the specified length
            var buffer = new byte[messageSize];
            for (int i = 0; i < messageSize - 1; i++)
            {
                buffer[i] = 0xFF;
            }

            buffer[messageSize - 1] = 0;
            return new ReadOnlyMemory<byte>(buffer);
        }

        public async Task Run()
        {
            Trace("Handler running");

            try
            {
                if (_isClient)
                {
                    // Send message to peer
                    await _stream.WriteAsync(_writeBuffer);
                }

                while (true)
                {
                    // Receive 0-terminated message from peer
                    while (true)
                    {
                        int count = await _stream.ReadAsync(_readBuffer);
                        if (count == 0)
                        {
                            throw new IOException("EOF from peer");
                        }

                        // Find 0 terminator, if present
                        // TODO: Not sure why the compiler wants me to cast here, but whatever
                        int index = _readBuffer.Span.Slice(0, count).IndexOf((byte)0);
                        if (index >= 0)
                        {
                            if (index != count - 1)
                            {
                                throw new Exception("Unexpected trailing data received");
                            }
                            break;
                        }
                    }

                    _requestCount++;

                    // Send message to peer
                    await _stream.WriteAsync(_writeBuffer);
                }
            }
            catch (IOException e)
            {
                if (_isClient)
                {
                    Console.WriteLine($"ERROR: Caught IO exception {e} in ClientHandler");
                    Environment.Exit(-1);
                }
                else
                {
                    // IO error when trying to receive from client.
                    // Shut down this handler.
                    Trace($"Caught IO exception {e} in ServerHandler");
                    Dispose();
                    return;
                }
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        [Conditional("PERFTRACE")]
        private void Trace(string s)
        {
            Console.WriteLine(s);
        }
    }
}
