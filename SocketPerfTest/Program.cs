using CommandLine;
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace SslStreamPerf
{
    class Program
    {
        private class BaseOptions
        {
            [Option('s', "messageSize", Default = 256, HelpText = "Message size to send")]
            public int MessageSize { get; set; }

            [Option("ssl", Default = false, HelpText = "Use SSL")]
            public bool UseSsl { get; set; }
        }

        private class BaseClientOptions : BaseOptions
        {
            [Option('c', "connections", Default = 256)]
            public int Clients { get; set; }

            [Option('w', "warmupTime", Default = 5)]            // Seconds
            public int WarmupTime { get; set; }

            [Option('i', "reportingInterval", Default = 3)]     // Seconds
            public int ReportingInterval { get; set; }

            [Option('n', "numberOfRequests", Default = 0, HelpText = "Total number of requests if positive number")]      // count
            public int NumberOfRequests { get; set; }

            [Option('t', "durationTime", Default = 0, HelpText = "Duration of test in seconds if positive number")]          // seconds
            public int DurationTime { get; set; }
        }

        [Verb("client", HelpText = "Run client using specified IP/port, e.g. '1.2.3.4:5000'.")]
        private class ClientOptions : BaseClientOptions
        {
            [Option('e', "endPoint", Required = true)]
            public string EndPoint { get; set; }
        }

        [Verb("server", HelpText = "Run server using specified IP/port, e,g, '1.2.3.4:5000'.")]
        private class ServerOptions : BaseOptions
        {
            [Option('e', "endPoint", Required = true)]
            public string EndPoint { get; set; }
        }

        [Verb("inproc", HelpText = "Run client and server in a single process over loopback.")]
        private class InProcOptions : BaseClientOptions
        {
        }

        [Verb("nonetwork", HelpText = "Run client and server in a single process over an in-memory stream.")]
        private class NoNetworkOptions : BaseClientOptions
        {
        }

        static IPEndPoint TryParseEndPoint(string s)
        {
            var splits = s.Split(':');
            if (splits.Length != 2)
            {
                return null;
            }

            IPAddress a;
            if (splits[0] == "*")
            {
                a = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(splits[0], out a))
                {
                    return null;
                }
            }

            if (!int.TryParse(splits[1], out int port))
            {
                return null;
            }

            if (port == 0)
            {
                return null;
            }

            return new IPEndPoint(a, port);
        }

        static int GetCurrentRequestCount(ClientHandler[] clientHandlers)
        {
            int total = 0;
            foreach (var c in clientHandlers)
            {
                total += c.RequestCount;
            }

            return total;
        }

        static void ShowResults(BaseClientOptions options, ClientHandler[] clientHandlers)
        {
            Console.WriteLine($"Test running with {options.Clients} clients and MessageSize={options.MessageSize}");
            Console.WriteLine(options.UseSsl ? "Using SSL" : "Using raw sockets (no SSL)");

            int countAfterWarmup = 0;
            if (options.WarmupTime != 0)
            {
                Console.WriteLine($"Waiting {options.WarmupTime} seconds for warmup");
                Thread.Sleep(options.WarmupTime * 1000);
                Console.WriteLine("Warmup complete");
                countAfterWarmup = GetCurrentRequestCount(clientHandlers);
            }

            int elapsed = 0;
            int previousCount = countAfterWarmup;
            int ReportingInterval = options.ReportingInterval > 0 ? options.ReportingInterval : 1;
            double currentRPS;
            double averageRPS;

            while (true)
            {
                Thread.Sleep(ReportingInterval * 1000);

                elapsed += ReportingInterval;
                int currentCount = GetCurrentRequestCount(clientHandlers);

                currentRPS = (currentCount - previousCount) / (double)options.ReportingInterval;
                averageRPS = (currentCount - countAfterWarmup) / (double)elapsed;

                if (options.ReportingInterval > 0)
                {
                    Console.WriteLine($"Elapsed time: {TimeSpan.FromSeconds(elapsed)}    Current RPS: {currentRPS:0.0}    Average RPS: {averageRPS:0.0}");
                }

                previousCount = currentCount;

                if ( options.NumberOfRequests > 0 && options.NumberOfRequests <= currentCount ) {
                    break;
                }
                if ( options.DurationTime > 0 && options.DurationTime <= elapsed ) {
                    break;
                }
            }
            if (options.ReportingInterval <= 0)
            {
                // write out final stats if we are asked not to do it interactively.
                Console.WriteLine($"Elapsed time: {TimeSpan.FromSeconds(elapsed)}    Average RPS: {averageRPS:0.0}");
            }
        }

        static X509Certificate2 GetX509Certificate(BaseOptions options) =>
            options.UseSsl ? SslHelper.LoadCert() : null;

        static int RunClient(ClientOptions options)
        {
            IPEndPoint endPoint = TryParseEndPoint(options.EndPoint);
            if (endPoint == null)
            {
                Console.WriteLine("Could not parse endpoint");
                return -1;
            }

            Console.WriteLine($"Running client to {endPoint}");

            ClientHandler[] clientHandlers = ClientRunner.Run(endPoint, options.UseSsl, options.Clients, options.MessageSize);

            ShowResults(options, clientHandlers);

            return 1;
        }

        static int RunServer(ServerOptions options)
        {
            IPEndPoint endPoint = TryParseEndPoint(options.EndPoint);
            if (endPoint == null)
            {
                Console.WriteLine("Could not parse endpoint");
                return -1;
            }

            Console.WriteLine($"Running server on {endPoint}");

            IPEndPoint serverEndpoint = ServerListener.Run(endPoint, GetX509Certificate(options), options.MessageSize);

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);

            return 1;
        }

        static int RunInProc(InProcOptions options)
        {
            Console.WriteLine("Running in-process over loopback");

            IPEndPoint serverEndpoint = ServerListener.Run(new IPEndPoint(IPAddress.Loopback, 0), GetX509Certificate(options), options.MessageSize);

            ClientHandler[] clientHandlers = ClientRunner.Run(serverEndpoint, options.UseSsl, options.Clients, options.MessageSize);

            ShowResults(options, clientHandlers);

            return 1;
        }

        static int RunNoNetwork(NoNetworkOptions options)
        {
            Console.WriteLine("Running in-process over in-memory stream");

            ClientHandler[] clientHandlers = NoNetworkTest.Run(GetX509Certificate(options), options.Clients, options.MessageSize);

            ShowResults(options, clientHandlers);

            return 1;
        }

        static int Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.HelpWriter = Console.Out;
            });

            return parser.ParseArguments<ClientOptions, ServerOptions, InProcOptions, NoNetworkOptions>(args).MapResult(
                (ClientOptions opts) => RunClient(opts),
                (ServerOptions opts) => RunServer(opts),
                (InProcOptions opts) => RunInProc(opts),
                (NoNetworkOptions opts) => RunNoNetwork(opts),
                _ => 1
            );
        }
    }
}
