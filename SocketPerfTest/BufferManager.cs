using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Buffers;
using System.Threading;
using System.Runtime.InteropServices;

namespace SslStreamPerf
{
    internal sealed class BufferManager
    {
        private readonly int _bufferSize;
        private readonly ConcurrentBag<BufferManagerBuffer> _bufferManagerBuffers;

        public BufferManager(int bufferSize = 4096)
        {
            _bufferSize = bufferSize;
            _bufferManagerBuffers = new ConcurrentBag<BufferManagerBuffer>();
        }

        public int BufferSize => _bufferSize;

        private const int BuffersPerAlloc = 64;

        public Memory<byte> GetBuffer()
        {
            BufferManagerBuffer buffer;
            if (_bufferManagerBuffers.TryTake(out buffer))
            {
                return buffer.Memory;
            }

            // Allocate more 
            // TODO: Be smarter here, as multiple threads could cause allocation at once

            IntPtr nativeAlloc = Marshal.AllocHGlobal(_bufferSize + BuffersPerAlloc);
            for (int i = 1; i < BuffersPerAlloc; i++)
            {
                _bufferManagerBuffers.Add(new BufferManagerBuffer(this, nativeAlloc + i * _bufferSize));
            }

            return new BufferManagerBuffer(this, nativeAlloc).Memory;
        }

        private void ReturnToPool(BufferManagerBuffer buffer)
        {
            _bufferManagerBuffers.Add(buffer);
        }

        class BufferManagerBuffer : OwnedMemory<byte>
        {
            private readonly IntPtr _nativePointer;
            private readonly BufferManager _bufferManager;
            private int _refCount;

            public BufferManagerBuffer(BufferManager bufferManager, IntPtr nativePointer)
            {
                _bufferManager = bufferManager;
                _nativePointer = nativePointer;

                _refCount = 1;
            }

            public override bool IsDisposed => _refCount == 0;

            protected override bool IsRetained => _refCount != 0;

            public override int Length => Span.Length;

            public override unsafe Span<byte> Span
            {
                get
                {
                    if (IsDisposed)
                        throw new ObjectDisposedException(nameof(BufferManagerBuffer));

                    return new Span<byte>((void*)_nativePointer, _bufferManager.BufferSize);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _bufferManager.ReturnToPool(this);
                }
            }

            public override unsafe MemoryHandle Pin(int byteOffset = 0)
            {
                if (byteOffset != 0)
                    throw new NotSupportedException("Pin: byteOffset != 0.  What does this mean?");

                return new MemoryHandle(this, (void*)_nativePointer);
            }

            public override bool Release()
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(nameof(BufferManagerBuffer));

                if (Interlocked.Decrement(ref _refCount) == 0)
                    Dispose();

                return true;        //?
            }

            public override void Retain()
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(nameof(BufferManagerBuffer));

                Interlocked.Increment(ref _refCount);
            }

            protected override bool TryGetArray(out ArraySegment<byte> arraySegment)
            {
                arraySegment = default(ArraySegment<byte>);
                return false;
            }
        }
    }
}
