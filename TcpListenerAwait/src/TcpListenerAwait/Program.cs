using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpListenerAwait
{
    public class Program
    {
        public static TcpListener s_listener;

        public const bool s_trace = true;

        public static readonly byte[] s_responseMessage = Encoding.UTF8.GetBytes("Hello world!\n");

        class Connection
        {
            private TcpClient _client;
            private byte[] _readBuffer = new byte[4096];

            public Connection()
            {
            }

            public async void Run()
            {
                _client = await s_listener.AcceptTcpClientAsync();

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _client.NoDelay = true;

                var stream = _client.GetStream();

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);
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

                    await stream.WriteAsync(s_responseMessage, 0, s_responseMessage.Length);

                    if (s_trace)
                    {
                        Console.WriteLine("Write complete");
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
            s_listener = new TcpListener(IPAddress.Any, 5000);
            s_listener.Start(1000);

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

