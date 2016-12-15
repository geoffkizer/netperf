using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SocketEventTask
{
    public static class SocketTaskExtensions
    {
        private static readonly EventHandler<SocketAsyncEventArgs> CompletionDelegate = CompletionCallback;

        private static void CompletionCallback(object sender, SocketAsyncEventArgs e)
        {
            TaskCompletionSource<bool> tcs = (TaskCompletionSource<bool>)e.UserToken;

            e.Completed -= CompletionDelegate;

            if (e.SocketError != SocketError.Success)
            {
                tcs.SetException(new SocketException((int)e.SocketError));
            }
            else
            {
                tcs.SetResult(true);
            }

        }

        private static Task SetupCallback(SocketAsyncEventArgs e)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            e.UserToken = tcs;
            e.Completed += CompletionDelegate;

            return tcs.Task;
        }

        public static Task AcceptAsync2(this Socket socket, SocketAsyncEventArgs e)
        {
            Task t = SetupCallback(e);

            bool pending = socket.AcceptAsync(e);
            if (!pending)
                CompletionCallback(null, e);

            return t;
        }

        public static Task SendAsync2(this Socket socket, SocketAsyncEventArgs e)
        {
            Task t = SetupCallback(e);

            bool pending = socket.SendAsync(e);
            if (!pending)
                CompletionCallback(null, e);

            return t;
        }

        public static Task ReceiveAsync2(this Socket socket, SocketAsyncEventArgs e)
        {
            Task t = SetupCallback(e);

            bool pending = socket.ReceiveAsync(e);
            if (!pending)
                CompletionCallback(null, e);

            return t;
        }
    }
}
