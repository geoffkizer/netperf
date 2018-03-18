using CommandLine;
using System;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

// Stuff to do:
// (1) Move messageSize to base
// (2) Rework basic handing
//          -- Always use messageSize (client and server)
//          -- Combine code b/w client and server
// (3) Add buffer manager usage

namespace SslStreamPerf
{
    class Program
    {
        private class BaseOptions
        {
            [Option('s', "messageSize", Default = 256, HelpText = "Message size to send")]
            public int MessageSize { get; set; }
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

            [Option("ssl", Default = false, HelpText = "Use SSL")]
            public bool UseSsl { get; set; }
        }

        [Verb("server", HelpText = "Run server using specified IP/port, e,g, '1.2.3.4:5000'.")]
        private class ServerOptions : BaseOptions
        {
            [Option('e', "endPoint", Default = "*:5000")]
            public string EndPoint { get; set; }


            [Option("maxIOThreads", Default = 0)]
            public int MaxIOThreads { get; set; }
        }

        [Verb("client", HelpText = "Run client using specified IP/port, e.g. '1.2.3.4:5000'.")]
        private class ClientOptions : BaseClientOptions
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

        static long GetCurrentRequestCount(ClientHandler[] clientHandlers)
        {
            long total = 0;
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

            int reportingInterval = options.ReportingInterval > 0 ? options.ReportingInterval :
                                        options.DurationTime > 0 ? options.DurationTime : 1;

            long startCount = 0;
            if (options.WarmupTime != 0)
            {
                Console.WriteLine($"Waiting {options.WarmupTime} seconds for warmup");
                Thread.Sleep(options.WarmupTime * 1000);
                Console.WriteLine("Warmup complete");
                startCount = GetCurrentRequestCount(clientHandlers);
            }

            Stopwatch timer = new Stopwatch();
            timer.Start();

            TimeSpan previousElapsed = TimeSpan.Zero;
            long previousCount = startCount;
            double previousAverageRPS = 0;

            while (true)
            {
                Thread.Sleep(reportingInterval * 1000);

                TimeSpan totalElapsed = timer.Elapsed;
                long totalCount = GetCurrentRequestCount(clientHandlers);

                double currentRPS = (totalCount - previousCount) / (totalElapsed - previousElapsed).TotalSeconds;
                double averageRPS = (totalCount - startCount) / totalElapsed.TotalSeconds;

                if (options.ReportingInterval > 0)
                {
                    // Write out interval stats.
                    string drift = (previousAverageRPS == 0.0 ? "" :
                        $"Drift: {(((averageRPS - previousAverageRPS) / previousAverageRPS) * 100),8:0.00}%");

                    Console.WriteLine($"Elapsed time: {totalElapsed}    Current RPS: {currentRPS,10:####.0}    Average RPS: {averageRPS,10:####.0}    {drift}");
                }

                if ((options.NumberOfRequests > 0 && options.NumberOfRequests <= totalCount) ||
                    (options.DurationTime > 0 && options.DurationTime <= totalElapsed.TotalSeconds))
                {
                    // Write out final stats.
                    Console.WriteLine($"Total elapsed time: {totalElapsed}    Average RPS: {averageRPS,10:####.0}    Total requests: {totalCount - startCount}");

                    break;
                }

                previousCount = totalCount;
                previousElapsed = totalElapsed;
                previousAverageRPS = averageRPS;
            }

            timer.Stop();
        }

        static X509Certificate2 GetX509Certificate(BaseClientOptions options) =>
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

            if (options.MaxIOThreads != 0)
            {
                ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int _);
                if (!ThreadPool.SetMaxThreads(maxWorkerThreads, options.MaxIOThreads))
                {
                    Console.WriteLine("ThreadPool.SetMaxThreads failed");
                    return -1;
                }
            }

            Console.WriteLine($"Running server on {endPoint} (raw)");
            var server1 = new ServerListener(endPoint, null, options.MessageSize);
            server1.Start();

            IPEndPoint sslEndPoint = new IPEndPoint(endPoint.Address, endPoint.Port + 1);

            Console.WriteLine($"Running server on {sslEndPoint} (SSL)");
            var server2 = new ServerListener(endPoint, SslHelper.LoadCert(), options.MessageSize);
            server2.Start();

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);

            return 1;
        }

        static int RunInProc(InProcOptions options)
        {
            Console.WriteLine("Running in-process over loopback");

            var server = new ServerListener(new IPEndPoint(IPAddress.Loopback, 0), GetX509Certificate(options), options.MessageSize);
            server.Start();

            IPEndPoint serverEndpoint = server.EndPoint;

            ClientHandler[] clientHandlers = ClientRunner.Run(serverEndpoint, options.UseSsl, options.Clients, options.MessageSize);

            ShowResults(options, clientHandlers);

            return 1;
        }

        static int RunNoNetwork(NoNetworkOptions options)
        {
#if NETCOREAPP1_1
            Console.WriteLine("Not supported on 1.1");
            Environment.Exit(-1);
#else
            Console.WriteLine("Temporarily disabled");
            Environment.Exit(-1);
#if false

            Console.WriteLine("Running in-process over in-memory stream");

            ClientHandler[] clientHandlers = NoNetworkTest.Run(GetX509Certificate(options), options.Clients, options.MessageSize);

            ShowResults(options, clientHandlers);
#endif
#endif

            return 1;
        }

        private static BufferManager s_bufferManager = new BufferManager();

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
