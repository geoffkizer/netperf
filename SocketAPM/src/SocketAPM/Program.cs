using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketAPM
{
    public class Program
    {
        public static Socket s_listenSocket;

        public const bool s_trace = true;

        public static readonly byte[] s_responseMessage = Encoding.UTF8.GetBytes("Hello world!\n");

        class Connection
        {
            private AsyncCallback _acceptCallback;
            private AsyncCallback _readCallback;
            private AsyncCallback _writeCallback;

            private Socket _socket;
            private byte[] _readBuffer = new byte[4096];

            public Connection()
            {
                _acceptCallback = this.OnAccept;
                _readCallback = this.OnRead;
                _writeCallback = this.OnWrite;
            }

            public void DoAccept()
            {
                s_listenSocket.BeginAccept(_acceptCallback, null);
            }

            private void OnAccept(IAsyncResult ar)
            {
                _socket = s_listenSocket.EndAccept(ar);

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _socket.NoDelay = true;

                DoRead();
            }

            private void DoRead()
            {
                _socket.BeginReceive(_readBuffer, 0, _readBuffer.Length, SocketFlags.None, _readCallback, null);
            }

            private void OnRead(IAsyncResult ar)
            {
                int bytesRead = _socket.EndReceive(ar);

                if (bytesRead == 0)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Connection closed by client");
                    }

                    return;
                }

                if (s_trace)
                {
                    Console.WriteLine("Read complete, bytesRead = {0}", bytesRead);
                }

                // Do write now

                _socket.BeginSend(s_responseMessage, 0, s_responseMessage.Length, SocketFlags.None, _writeCallback, null);
            }

            private void OnWrite(IAsyncResult ar)
            {
                int bytesWritten = _socket.EndSend(ar);

                if (s_trace)
                {
                    Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                }

                DoRead();
            }
        }

        private static void HandleConnection(object state)
        {
            var c = new Connection();
            c.DoAccept();
        }

        private static void QueueConnectionHandler()
        {
            ThreadPool.QueueUserWorkItem(HandleConnection);
        }

        private static void Start()
        {
            s_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s_listenSocket.Bind(new IPEndPoint(IPAddress.Any, 5000));
            s_listenSocket.Listen(1000);

            QueueConnectionHandler();
        }

        public static void Main(string[] args)
        {
            Start();

            Console.WriteLine("Server Running");
            Console.ReadLine();
        }
    }
}

