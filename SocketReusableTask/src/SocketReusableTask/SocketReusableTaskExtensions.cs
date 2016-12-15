using System;
using System.Net.Sockets;

namespace SocketReusableTask
{
    public static class SocketReusableTaskExtensions
    {
        private static readonly EventHandler<SocketAsyncEventArgs> CompletionDelegate = CompletionCallback;

        private static void CompletionCallback(object sender, SocketAsyncEventArgs e)
        {
            ReusableTaskCompletionSource tcs = (ReusableTaskCompletionSource)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                tcs.SetException(new SocketException((int)e.SocketError));
            }
            else
            {
                tcs.SetResult();
            }
        }

        public static void SetupCallback(SocketAsyncEventArgs e)
        {
            ReusableTaskCompletionSource tcs = new ReusableTaskCompletionSource();
            e.UserToken = tcs;
            e.Completed += CompletionDelegate;
        }

        public static ReusableTask AcceptAsync2(this Socket socket, SocketAsyncEventArgs e)
        {
            bool pending = socket.AcceptAsync(e);
            if (!pending)
                CompletionCallback(null, e);

            return ((ReusableTaskCompletionSource)e.UserToken).Task;
        }

        public static ReusableTask SendAsync2(this Socket socket, SocketAsyncEventArgs e)
        {
            bool pending = socket.SendAsync(e);
            if (!pending)
                CompletionCallback(null, e);

            return ((ReusableTaskCompletionSource)e.UserToken).Task;
        }

        public static ReusableTask ReceiveAsync2(this Socket socket, SocketAsyncEventArgs e)
        {
            bool pending = socket.ReceiveAsync(e);
            if (!pending)
                CompletionCallback(null, e);

            return ((ReusableTaskCompletionSource)e.UserToken).Task;
        }
    }
}
