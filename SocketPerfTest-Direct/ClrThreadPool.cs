using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Threading;

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
    }
}
