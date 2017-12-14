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
            if (!Interop.SetFileCompletionNotificationModes(s.Handle, 
                Interop.FileCompletionNotificationModes.SkipCompletionPortOnSuccess | Interop.FileCompletionNotificationModes.SkipSetEventOnHandle))
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
                    // Retrieve actual error code
                    if (Interop.WSAGetOverlappedResult(boundHandle.Handle.DangerousGetHandle(), nativeOverlapped, out _, false, out _))
                    {
                        callback(SocketError.Success, (int)bytesTransferred);
                    }
                    else
                    {
                        SocketError socketError = SocketDirect.GetLastSocketError();
                        Debug.Assert(socketError != SocketError.IOPending);
                        Debug.Assert(socketError != SocketError.Success);

                        callback(socketError, 0);
                    }
                }
            }, null, null);
        }
    }
}
