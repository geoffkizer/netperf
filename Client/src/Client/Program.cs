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
        public static int s_duration = -1;  // Duration to run, in seconds.  -1 = forever.
        public static IPAddress s_ipAddress = IPAddress.Loopback;
        public static int s_port = 5000;

        // TODO: Should probably send the actual TechEmpower quest here
        public static readonly byte[] s_requestMessage = new byte[848];

        public const int s_expectedReadSize = 1568;

        class Connection
        {
            private readonly Socket _socket;

            private readonly SocketAsyncEventArgs _sendEventArgs;
            private readonly SocketAsyncEventArgs _receiveEventArgs;
            private int _bytesReceived;
            private int _requestsProcessed = 0;

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

            public int RequestsProcessed => _requestsProcessed;

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

                _requestsProcessed++;

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

            Connection[] connections = new Connection[s_connectionCount];

            for (int i = 0; i < s_connectionCount; i++)
            {
                // Do sync connect for simplicity
                Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(s_ipAddress, s_port));

                connections[i] = new Connection(socket);
            }

            Console.WriteLine("All connections established");

            for (int i = 0; i < s_connectionCount; i++)
            {
                Connection c = connections[i];
                Task.Run(() => c.Run());
            }

            if (s_duration != -1)
            {
                Console.WriteLine("Client running for {0} seconds", s_duration);

                int totalRequests = 0;

                // Let it run the specified amount
                Thread.Sleep(s_duration * 1000);

                // Accumulate total # of requests processed (approximately)
                for (int i = 0; i < s_connectionCount; i++)
                {
                    totalRequests += connections[i].RequestsProcessed;
                }

                Console.WriteLine("Processed {0} requests in {1} seconds", totalRequests, s_duration);
                Console.WriteLine("Requests per second: {0:F1}", ((double)totalRequests) / s_duration);
            }
            else
            {
                Console.WriteLine("Client running");
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: client [serverIP:Port] [-c <value>] [-d <value>] [-t]");
            Console.WriteLine("    -c <value>     Set connection count");
            Console.WriteLine("    -d <value>     Duration to run, in seconds. Default is run forever.");
            Console.WriteLine("    -t             Enable trace output");
        }

        private static bool ParseArgs(string[] args)
        {
            int i = 0;
            while (i < args.Length)
            {
                if (args[i][0] != '-')
                {
                    // Parse as IP address/port
                    string[] parts = args[i].Split(':');
                    if (parts.Length != 2)
                    {
                        Console.WriteLine("Could not parse ip address/port");
                        PrintUsage();
                        return false;
                    }

                    if (!IPAddress.TryParse(parts[0], out s_ipAddress))
                    {
                        Console.WriteLine("Could not parse ip address");
                        PrintUsage();
                        return false;
                    }

                    if (!int.TryParse(parts[1], out s_port))
                    {
                        Console.WriteLine("Could not parse port");
                        PrintUsage();
                        return false;
                    }
                }
                else if (args[i] == "-c")
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
                else if (args[i] == "-d")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.WriteLine("Missing value for parameter");
                        PrintUsage();
                        return false;
                    }

                    if (!int.TryParse(args[i], out s_duration))
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
