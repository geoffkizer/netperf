using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace SocketPerfTest
{
    public static class ClrThreadPool
    {
        // move, probably
        [DllImport("kernel32.dll")]
        private static unsafe extern bool SetFileCompletionNotificationModes(
            IntPtr handle,
            FileCompletionNotificationModes flags);

        [Flags]
        private enum FileCompletionNotificationModes : byte
        {
            None = 0,
            SkipCompletionPortOnSuccess = 1,
            SkipSetEventOnHandle = 2
        }

        private class UnownedSocketHandle : SafeHandle
        {
            public UnownedSocketHandle(Socket socket)
                : base(socket.Handle, ownsHandle: false)
            {
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            protected override bool ReleaseHandle() => true;
        }

        public static ThreadPoolBoundHandle Bind(Socket s)
        {
            // Bind to CLR thread pool
            ThreadPoolBoundHandle h = ThreadPoolBoundHandle.BindHandle(new UnownedSocketHandle(s));

            // Set completion mode
            if (!SetFileCompletionNotificationModes(s.Handle, FileCompletionNotificationModes.SkipCompletionPortOnSuccess | FileCompletionNotificationModes.SkipSetEventOnHandle))
            {
                throw new Exception("SetFileCompletionNotificationModes failed");
            }

            return h;
        }

        public static unsafe PreAllocatedOverlapped CreatePreAllocatedOverlapped(ThreadPoolBoundHandle boundHandle, Action<SocketError, int> callback)
        {
            // Note this will allocate for the delegate state.
            // We don't care because we assume this is called infrequently -- i.e. per Socket, not per operation
            return new PreAllocatedOverlapped((uint error, uint bytesTransferred, NativeOverlapped* nativeOverlapped) =>
            {
                boundHandle.FreeNativeOverlapped(nativeOverlapped);

                if (error == 0)
                {
                    callback(SocketError.Success, (int)bytesTransferred);
                }
                else
                {
                    // This isn't right, we need to call WSAGetOverlappedResult or whatever

                    SocketError socketError = SocketDirect.GetLastSocketError();
                    Debug.Assert(socketError != SocketError.IOPending);
                    Debug.Assert(socketError != SocketError.Success);

                    callback(socketError, 0);
                }
            }, null, null);
        }
    }
}
