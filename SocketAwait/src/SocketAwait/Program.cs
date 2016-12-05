using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketAwait
{
    public class Program
    {
        public static Socket s_listenSocket;

        public const bool s_trace = true;

        public static readonly byte[] s_responseMessage = Encoding.UTF8.GetBytes("Hello world!\n");

        class Connection
        {
            private Socket _socket;
            private byte[] _readBuffer = new byte[4096];

            public Connection()
            {
            }

            public async void Run()
            {
                _socket = await s_listenSocket.AcceptAsync();

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _socket.NoDelay = true;

                while (true)
                {
                    int bytesRead = await _socket.ReceiveAsync(new ArraySegment<byte>(_readBuffer), SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        if (s_trace)
                        {
                            Console.WriteLine("Connection closed by client");
                        }

                        break;
                    }

                    if (s_trace)
                    {
                        Console.WriteLine("Read complete, bytesRead = {0}", bytesRead);
                    }

                    int bytesWritten = await _socket.SendAsync(new ArraySegment<byte>(s_responseMessage), SocketFlags.None);

                    if (s_trace)
                    {
                        Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                    }
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

