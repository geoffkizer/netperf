using System;

namespace SslStreamPerf
{
    internal class ConnectionManager
    {
        private readonly int _messageSize;
        private readonly BufferManager _readBufferManager;
        private readonly BufferManager _writeBufferManager;

        public ConnectionManager(int messageSize)
        {
            _messageSize = messageSize;

            _readBufferManager = new BufferManager(4096);

            int writeBufferSize = messageSize;
            if (messageSize < 4096)
                writeBufferSize = 4096;

            _writeBufferManager = new BufferManager(writeBufferSize);
        }

        public int MessageSize => _messageSize;

        public Memory<byte> GetReadBuffer()
        {
            return _readBufferManager.GetBuffer();
        }

        public ReadOnlyMemory<byte> GetWriteBuffer()
        {
            Memory<byte> writeBuffer = _writeBufferManager.GetBuffer();

            // Create zero-terminated message of the specified length
            writeBuffer.Span.Fill(0xFF);
            writeBuffer.Span[_messageSize - 1] = 0;

            return writeBuffer.Slice(0, _messageSize);
        }
    }
}
