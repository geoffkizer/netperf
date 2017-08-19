using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal abstract class BaseHandler : IDisposable
    {
        private const int BufferSize = 4096;

        private readonly Stream _stream;
        private readonly byte[] _readBuffer;
        private readonly byte[] _writeBuffer;
        private int _readOffset;
        private int _readCount;

        public BaseHandler(Stream stream, int messageSize)
        {
            _stream = stream;

            _readBuffer = new byte[BufferSize];
            _readOffset = 0;
            _readCount = 0;

            // Create zero-terminated message of the specified length
            _writeBuffer = new byte[messageSize];
            Array.Fill(_writeBuffer, (byte)0xFF);
            _writeBuffer[messageSize - 1] = 0;
        }

        private bool TryReadMessage()
        {
            if (_readCount == 0)
            {
                return false;
            }

            int index = Array.IndexOf<byte>(_readBuffer, 0, _readOffset, _readCount);
            if (index < 0)
            {
                return false;
            }

            _readOffset += index + 1;
            _readCount -= index + 1;
            return true;
        }

        protected async Task<bool> ReceiveMessage()
        {
            while (!TryReadMessage())
            {
                _readOffset = 0;
                _readCount = await _stream.ReadAsync(_readBuffer, 0, BufferSize);
                if (_readCount == 0)
                {
                    Trace("Connection closed by client");
                    return false;   // EOF
                }

                Trace($"Read complete, bytesRead = {_readCount}");
            }

            return true;
        }

        protected async Task SendMessage()
        {
            await _stream.WriteAsync(_writeBuffer, 0, _writeBuffer.Length);

            Trace("Write complete");
        }

        public abstract Task Run();

        public void Dispose()
        {
            _stream.Dispose();
        }

        [Conditional("PERFTRACE")]
        protected void Trace(string s)
        {
            Console.WriteLine(s);
        }
    }
}
