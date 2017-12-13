using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Buffers;

namespace SocketPerfTest
{
    internal sealed unsafe class ClrThreadServerHandler
    {
        private const int ReadBufferSize = 4 * 1024;

        private readonly Socket _socket;
        private readonly IntPtr _socketHandle;

        private readonly byte[] _readBuffer;
        private readonly OwnedMemory<byte> _readBufferMemory;
        private readonly byte* _readBufferPtr;

        private int _messageByteCount;

        private byte[] _writeBuffer;
        private OwnedMemory<byte> _writeBufferMemory;
        private byte* _writeBufferPtr;

        private readonly ThreadPoolBoundHandle _boundHandle;
        private readonly PreAllocatedOverlapped _readOverlapped;
        private readonly PreAllocatedOverlapped _writeOverlapped;

        public ClrThreadServerHandler(Socket socket)
        {
            _socket = socket;
            _socketHandle = socket.Handle;

            _readBuffer = new byte[ReadBufferSize];
            _readBufferMemory = new PinnedMemory<byte>(_readBuffer);
            fixed (byte* p = &_readBufferMemory.Span.DangerousGetPinnableReference())
            {
                _readBufferPtr = p;
            }

            _boundHandle = ClrThreadPool.Bind(socket);

            _readOverlapped = ClrThreadPool.CreatePreAllocatedOverlapped(_boundHandle, OnRead);
            _writeOverlapped = ClrThreadPool.CreatePreAllocatedOverlapped(_boundHandle, OnWrite);

            _messageByteCount = 0;
        }

        public void Run()
        {
            Trace("ServerHandler running");
            DoRead();
        }

        private unsafe void DoRead()
        {
            int bytesTransferred;
            SocketFlags socketFlags = SocketFlags.None;
            NativeOverlapped* nativeOverlapped = _boundHandle.AllocateNativeOverlapped(_readOverlapped);
            SocketError socketError = SocketDirect.Receive(_socketHandle, _readBufferPtr, ReadBufferSize, out bytesTransferred, ref socketFlags, nativeOverlapped);
            if (socketError != SocketError.IOPending)
            {
                _boundHandle.FreeNativeOverlapped(nativeOverlapped);
                Trace("Read completed synchronously");
                OnRead(socketError, bytesTransferred);
            }
        }

        private unsafe void OnRead(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success)
            {
                Dispose();
                if (socketError == SocketError.ConnectionReset)
                {
                    Trace("Connection reset by client");
                    return;
                }

                throw new Exception($"read failed, error = {socketError}");
            }

            if (bytesTransferred == 0)
            {
                Dispose();
                Trace("Connection closed by client");
                return;
            }

            Trace($"Read complete, bytes read = {bytesTransferred}");

            // Find 0 at end of message
            int index = Array.IndexOf<byte>(_readBuffer, 0, 0, bytesTransferred);
            if (index < 0)
            {
                // Consume all remaining bytes
                _messageByteCount += bytesTransferred;

                // Issue another read
                DoRead();
                return;
            }

            _messageByteCount += index + 1;
            if (_writeBuffer == null)
            {
                // First message received.
                // Construct a response message of the same size
                _writeBuffer = CreateMessageBuffer(_messageByteCount);
                _writeBufferMemory = new PinnedMemory<byte>(_writeBuffer);
                fixed (byte* p = &_writeBufferMemory.Span.DangerousGetPinnableReference())
                {
                    _writeBufferPtr = p;
                }
            }
            else
            {
                // We expect the same size message from the client every time, so check this.
                if (_messageByteCount != _writeBuffer.Length)
                {
                    Dispose();
                    throw new Exception($"Expected message size {_writeBuffer.Length} but received {_messageByteCount}");
                }
            }

            // We don't currently handle the case where multiple messages get sent at the same time
            if (index + 1 != bytesTransferred)
            {
                Dispose();
                throw new Exception($"Read more than a single message???");
            }

            DoWrite();
        }

        private void DoWrite()
        {
            int bytesTransferred;
            NativeOverlapped* nativeOverlapped = _boundHandle.AllocateNativeOverlapped(_writeOverlapped);
            SocketError socketError = SocketDirect.Send(_socketHandle, _writeBufferPtr, _messageByteCount, out bytesTransferred, SocketFlags.None, nativeOverlapped);
            if (socketError != SocketError.IOPending)
            {
                _boundHandle.FreeNativeOverlapped(nativeOverlapped);
                Trace("Write completed synchronously");
                OnWrite(socketError, bytesTransferred);
            }
        }

        private void OnWrite(SocketError socketError, int bytesTransferred)
        {
            if (socketError != SocketError.Success)
            {
                throw new Exception($"write failed, error = {socketError}");
            }

            if (bytesTransferred != _messageByteCount)
            {
                throw new Exception($"unexpected write size, bytes written = {bytesTransferred}");
            }

            Trace($"Write complete, bytes written = {bytesTransferred}");

            _messageByteCount = 0;
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
