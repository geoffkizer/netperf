using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using System.Runtime.InteropServices;

namespace SslStreamPerf
{
    internal sealed unsafe class PinnedMemory<T> : OwnedMemory<T>
    {
        private T[] _array;
        private GCHandle _handle;
        void* _pointer;

        public PinnedMemory(T[] array)
        {
            _array = array;
            _handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
            _pointer = (void*) Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
        }

        protected override bool TryGetArray(out ArraySegment<T> arraySegment)
        {
            arraySegment = new ArraySegment<T>(_array);
            return true;
        }

        public override int Length => _array.Length;

        public override Span<T> Span => new Span<T>(_array);

        public override MemoryHandle Pin() => new MemoryHandle(this, _pointer);

        public override bool IsDisposed => false;
        protected override bool IsRetained => true;

        public override bool Release() => true;
        public override void Retain() { }

        protected override void Dispose(bool disposing) { }

    }
}
