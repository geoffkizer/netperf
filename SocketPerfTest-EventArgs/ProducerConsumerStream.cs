using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if !NETCOREAPP1_1

namespace SslStreamPerf
{
    public sealed class ProducerConsumerStream : Stream, IDisposable
    {
        private ProducerConsumerStream _partner;
        private TaskCompletionSource<ArraySegment<byte>> _readSignal;
        private TaskCompletionSource<bool> _writeSignal;
        private ArraySegment<byte> _bytes;
        private bool _isClosed;

        private ProducerConsumerStream()
        {
            _readSignal = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _writeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // We start out able to write but not read
            _writeSignal.SetResult(true);

            _isClosed = false;
        }

        public static (ProducerConsumerStream stream1, ProducerConsumerStream stream2) Create()
        {
            var s1 = new ProducerConsumerStream();
            var s2 = new ProducerConsumerStream();
            s1._partner = s2;
            s2._partner = s1;

            return (s1, s2);
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_isClosed)
            {
                throw new ObjectDisposedException("MemoryPipeStream");
            }

            if (buffer == null || offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentException();
            }

            if (count == 0)
            {
                return;
            }

            await _writeSignal.Task;
            _writeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _partner._readSignal.SetResult(new ArraySegment<byte>(buffer, offset, count));
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null || offset < 0 || count <= 0 || offset + count > buffer.Length)
            {
                throw new ArgumentException();
            }

            if (_bytes.Array == null)
            {
                _bytes = await _readSignal.Task;
                _readSignal = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            Debug.Assert(_bytes.Array != null);

            if (count > _bytes.Count)
            {
                count = _bytes.Count;
            }

            Buffer.BlockCopy(_bytes.Array, _bytes.Offset, buffer, offset, count);

            if (count == _bytes.Count)
            {
                _bytes = new ArraySegment<byte>();
                _partner._writeSignal.SetResult(true);
            }
            else
            {
                _bytes = new ArraySegment<byte>(_bytes.Array, _bytes.Offset + count, _bytes.Count - count);
            }

            return count;
        }

        public new void Dispose()
        {
            if (!_isClosed)
            {
                _isClosed = true;

                // TODO
                base.Dispose();
            }
        }

        public override void Flush() { }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
#endif
