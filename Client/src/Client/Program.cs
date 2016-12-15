using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    public class Program
    {
        public const bool s_trace = false;

        public static readonly byte[] s_requestMessage = Enumerable.Range(0, 848).Select(x => (byte)x).ToArray();

        public const int s_expectedReadSize = 1568;

        public const int s_connectionCount = 512;

        class Connection
        {
            private SocketAsyncEventArgs _connectEventArgs;
            private SocketAsyncEventArgs _sendEventArgs;
            private SocketAsyncEventArgs _receiveEventArgs;
            private int _bytesReceived;

            private Socket _socket;

            public Connection()
            {
                _connectEventArgs = new SocketAsyncEventArgs();
                _connectEventArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 5000);
                _connectEventArgs.Completed += OnConnect;

                _sendEventArgs = new SocketAsyncEventArgs();
                _sendEventArgs.SetBuffer(s_requestMessage, 0, s_requestMessage.Length);
                _sendEventArgs.Completed += OnSend;

                _receiveEventArgs = new SocketAsyncEventArgs();
                _receiveEventArgs.SetBuffer(new byte[4096], 0, 4096);
                _receiveEventArgs.Completed += OnReceive;
            }

            public void Run()
            {
                bool pending = Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, _connectEventArgs);
                if (!pending)
                    OnConnect(null, _connectEventArgs);
            }

            private void OnConnect(object sender, SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    throw new Exception("connect failed");
                }

                if (s_trace)
                {
                    Console.WriteLine("Connection established");
                }

                _socket = e.ConnectSocket;
                _socket.NoDelay = true;

                DoSend();
            }

            private void DoSend()
            {
                bool pending = _socket.SendAsync(_sendEventArgs);
                if (!pending)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Send completed synchronously");
                    }

                    OnSend(null, _sendEventArgs);
                }
            }

            private void OnSend(object sender, SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    throw new Exception("Send failed");
                }

                int bytesWritten = e.BytesTransferred;

                if (bytesWritten != s_requestMessage.Length)
                {
                    throw new Exception(string.Format("unexpected write size, bytesWritten = {0}", bytesWritten));
                }

                if (s_trace)
                {
                    Console.WriteLine("Send complete, bytesWritten = {0}", bytesWritten);
                }

                // Do receive now
                _bytesReceived = 0;
                DoReceive();
            }

            private void DoReceive()
            { 
                bool pending = _socket.ReceiveAsync(_receiveEventArgs);
                if (!pending)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Receive completed synchronously");
                    }

                    OnReceive(null, _receiveEventArgs);
                }
            }

            private void OnReceive(object sender, SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    if (e.SocketError == SocketError.ConnectionReset)
                    {
                        _socket.Dispose();
                        return;
                    }

                    throw new Exception(string.Format("Receive failed, error = {0}", e.SocketError));
                }

                int bytesRead = e.BytesTransferred;

                if (bytesRead == 0)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Connection closed by server");
                    }

                    _socket.Dispose();
                    return;
                }

                if (s_trace)
                {
                    Console.WriteLine("Receive complete, bytesRead = {0}", bytesRead);
                }

                _bytesReceived += bytesRead;

                if (_bytesReceived > s_expectedReadSize)
                {
                    throw new Exception(string.Format("unexpected receive size, bytesReceived = {0}", _bytesReceived));
                }
                else if (_bytesReceived < s_expectedReadSize)
                {
                    // Didn't receive the whole message -- do another receive
                    DoReceive();
                }
                else
                {
                    // Full message received, time to Send again
                    DoSend();
                }
            }
        }

        private static void HandleConnection(object state)
        {
            var c = new Connection();
            c.Run();
        }

        private static void QueueConnectionHandler()
        {
            ThreadPool.QueueUserWorkItem(HandleConnection);
        }

        private static void Start()
        {
            for (int i = 0; i < s_connectionCount; i++)
                QueueConnectionHandler();
        }

        public static void Main(string[] args)
        {
            Start();

            Console.WriteLine("Client Running");
            Console.ReadLine();
        }
    }
}
