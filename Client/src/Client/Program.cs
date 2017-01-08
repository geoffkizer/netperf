using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    public class Program
    {
        // Command line settable parameters
        public static int s_connectionCount = 512;
        public static bool s_trace = false;

        // TODO: Should probably send the actual TechEmpower quest here
        public static readonly byte[] s_requestMessage = new byte[848];

        public const int s_expectedReadSize = 1568;

        class Connection
        {
            private readonly Socket _socket;

            private readonly SocketAsyncEventArgs _sendEventArgs;
            private readonly SocketAsyncEventArgs _receiveEventArgs;
            private int _bytesReceived;

            public Connection(Socket socket)
            {
                _socket = socket;
                _socket.NoDelay = true;

                _sendEventArgs = new SocketAsyncEventArgs();
                _sendEventArgs.SetBuffer(s_requestMessage, 0, s_requestMessage.Length);
                _sendEventArgs.Completed += OnSend;

                _receiveEventArgs = new SocketAsyncEventArgs();
                _receiveEventArgs.SetBuffer(new byte[4096], 0, 4096);
                _receiveEventArgs.Completed += OnReceive;
            }

            public void Run()
            {
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
                    throw new Exception(string.Format("Receive failed, error = {0}", e.SocketError));
                }

                int bytesRead = e.BytesTransferred;
                if (bytesRead == 0)
                {
                    throw new Exception("Connection closed by server");
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

        private static void HandleConnection(Socket socket)
        {
            var c = new Connection(socket);
            c.Run();
        }

        private static void Start()
        {
            Console.WriteLine("Establishing {0} connections...", s_connectionCount);

            for (int i = 0; i < s_connectionCount; i++)
            {
                // Do sync connect for simplicity
                Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(IPAddress.Loopback, 5000));

                Console.WriteLine("Connection #{0} established", (i+1));

                Task.Run(() => HandleConnection(socket));
            }

            Console.WriteLine("All connections established, client running");
            Console.ReadLine();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: client [-c <value>] [-t]");
            Console.WriteLine("    -c <value>     Set connection count");
            Console.WriteLine("    -t             Enable trace output");
        }

        private static bool ParseArgs(string[] args)
        {
            int i = 0;
            while (i < args.Length)
            {
                if (args[i] == "-c")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.WriteLine("Missing value for parameter");
                        PrintUsage();
                        return false;
                    }

                    if (!int.TryParse(args[i], out s_connectionCount))
                    {
                        Console.WriteLine("Could not parse parameter value {0}", args[i]);
                        PrintUsage();
                        return false;
                    }
                }
                else if (args[i] == "-t")
                {
                    s_trace = true;
                }
                else
                {
                    Console.WriteLine("Unknown parameter {0}", args[i]);
                    PrintUsage();
                    return false;
                }

                i++;
            }

            return true;
        }

        public static void Main(string[] args)
        {
            if (!ParseArgs(args))
                return;

            Start();
        }
    }
}
