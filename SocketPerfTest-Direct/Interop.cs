using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net.Sockets;

namespace SocketPerfTest
{
    internal static class Interop
    {
        [DllImport("kernel32.dll")]
        internal static unsafe extern bool SetFileCompletionNotificationModes(
            IntPtr handle,
            FileCompletionNotificationModes flags);

        [Flags]
        internal enum FileCompletionNotificationModes : byte
        {
            None = 0,
            SkipCompletionPortOnSuccess = 1,
            SkipSetEventOnHandle = 2
        }


        [DllImport("ws2_32", SetLastError = true)]
        internal static unsafe extern bool WSAGetOverlappedResult(
            IntPtr socketHandle,
            NativeOverlapped* overlapped,
            out uint bytesTransferred,
            bool wait,
            out SocketFlags socketFlags);

    }
}
