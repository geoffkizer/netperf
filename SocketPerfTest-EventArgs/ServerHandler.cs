using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Diagnostics;

namespace SslStreamPerf
{
    internal sealed class ServerHandler
    {
        private const int ReadBufferSize = 4 * 1024;

        private readonly Socket _socket;

        private readonly byte[] _readBuffer;
        private int _messageByteCount;

        private byte[] _messageBuffer;

        private SocketAsyncEventArgs _readEventArgs;
        private SocketAsyncEventArgs _writeEventArgs;

        public ServerHandler(Socket socket)
        {
            _socket = socket;

            _readBuffer = new byte[ReadBufferSize];

            _readEventArgs = new SocketAsyncEventArgs();
            _readEventArgs.SetBuffer(new Memory<byte>(_readBuffer));
            _readEventArgs.Completed += OnRead;

            _writeEventArgs = new SocketAsyncEventArgs();
            _writeEventArgs.Completed += OnWrite;
        }

        public void Run()
        {
            Trace("ServerHandler running");
            DoRead();
        }

        private void DoRead()
        {
            _messageByteCount = 0;

            bool pending = _socket.ReceiveAsync(_readEventArgs);
            if (!pending)
            {
                Trace("Read completed synchronously");
                OnRead(null, _readEventArgs);
            }
        }

        private void OnRead(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Dispose();
                if (e.SocketError == SocketError.ConnectionReset)
                {
                    Trace("Connection reset by client");
                    return;
                }

                throw new Exception($"read failed, error = {e.SocketError}");
            }

            int bytesRead = e.BytesTransferred;
            if (bytesRead == 0)
            {
                Dispose();
                Trace("Connection closed by client");
                return;
            }

            Trace($"Read complete, bytesRead = {bytesRead}");

            // Find 0 at end of message
            int index = Array.IndexOf<byte>(_readBuffer, 0, 0, bytesRead);
            if (index < 0)
            {
                // Consume all remaining bytes
                _messageByteCount += bytesRead;

                // Issue another read
                bool readPending = _socket.ReceiveAsync(_readEventArgs);
                if (!readPending)
                {
                    Trace("Read completed synchronously");
                    OnRead(null, _readEventArgs);
                }

                return;
            }

            _messageByteCount += index + 1;
            if (_messageBuffer == null)
            {
                // First message received.
                // Construct a response message of the same size
                _messageBuffer = CreateMessageBuffer(_messageByteCount);
                _writeEventArgs.SetBuffer(new Memory<byte>(_messageBuffer));
            }
            else
            {
                // We expect the same size message from the client every time, so check this.
                if (_messageByteCount != _messageBuffer.Length)
                {
                    Dispose();
                    throw new Exception($"Expected message size {_messageBuffer.Length} but received {_messageByteCount}");
                }
            }

            // We don't currently handle the case where multiple messages get sent at the same time
            if (index + 1 != bytesRead)
            {
                Dispose();
                throw new Exception($"Read more than a single message???");
            }

            // Do write now
            bool writePending = _socket.SendAsync(_writeEventArgs);
            if (!writePending)
            {
                OnWrite(null, _writeEventArgs);
            }
        }

        private void OnWrite(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                throw new Exception($"write failed, error = {e.SocketError}");
            }

            int bytesWritten = e.BytesTransferred;
            if (bytesWritten != _messageByteCount)
            {
                throw new Exception($"unexpected write size, bytesWritten = {bytesWritten}");
            }

            Trace($"Write complete, bytesWritten = {bytesWritten}");

            DoRead();
        }

        private byte[] CreateMessageBuffer(int messageSize)
        {
            // Create zero-terminated message of the specified length
            var buffer = new byte[messageSize];
            for (int i = 0; i < messageSize - 1; i++)
            {
                buffer[i] = 0xFF;
            }

            buffer[messageSize - 1] = 0;
            return buffer;
        }

        private void Dispose()
        {
            _socket.Close();
        }

        [Conditional("PERFTRACE")]
        private void Trace(string s)
        {
            Console.WriteLine(s);
        }
    }
}
