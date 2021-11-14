using CommandLine;
using System;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SslStreamPerf
{
    class Program
    {
        [Verb("client", HelpText = "Run client.")]
        private class ClientOptions
        {
            [Option('e', "endPoint", Default = "127.0.0.1:5000", HelpText = "Server endpoint to connect to.")]
            public string EndPoint { get; set; }

            [Option('s', "messageSize", Default = 256, HelpText = "Size of request message to send to server.")]
            public int MessageSize { get; set; }

            [Option("ssl", Default = false, HelpText = "Use SSL.")]
            public bool UseSsl { get; set; }

            [Option('c', "connections", Default = 256, HelpText = "Number of connections to establish to server.")]
            public int Connections { get; set; }

            [Option('w', "warmupTime", Default = 5, HelpText = "Warmup time in seconds.")]
            public int WarmupTime { get; set; }

            [Option('i', "reportingInterval", Default = 3, HelpText = "Reporting interval in seconds.")]
            public int ReportingInterval { get; set; }

            [Option('t', "durationTime", Default = 0, HelpText = "Duration of test in seconds (0 means infinite)")]
            public int DurationTime { get; set; }
        }

        [Verb("server", HelpText = "Run server.")]
        private class ServerOptions
        {
            [Option('e', "endPoint", Default = "127.0.0.1:5000", HelpText = "Local endpoint to listen on.")]
            public string EndPoint { get; set; }

            [Option('s', "messageSize", Default = 256, HelpText = "Size of response message to send to client.")]
            public int MessageSize { get; set; }

            [Option("ssl", Default = false, HelpText = "Use SSL.")]
            public bool UseSsl { get; set; }

            [Option("maxIOThreads", Default = 0)]
            public int MaxIOThreads { get; set; }
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

        static void ShowResults(ClientOptions options, ClientHandler[] clientHandlers)
        {
            Console.WriteLine($"Test running with {options.Connections} connections and MessageSize={options.MessageSize}");
            Console.WriteLine(options.UseSsl ? "Using SSL" : "Using sockets (no SSL)");

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

                if (options.DurationTime > 0 && options.DurationTime <= totalElapsed.TotalSeconds)
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

        static int RunClient(ClientOptions options)
        {
            if (!IPEndPoint.TryParse(options.EndPoint, out IPEndPoint endPoint))
            {
                Console.WriteLine($"Could not parse endpoint {options.EndPoint}");
                return -1;
            }

            Console.WriteLine($"Running client to {endPoint}");

            ClientHandler[] clientHandlers = ClientRunner.Run(endPoint, options.UseSsl, options.Connections, options.MessageSize);

            ShowResults(options, clientHandlers);

            return 1;
        }

        static int RunServer(ServerOptions options)
        {
            if (!IPEndPoint.TryParse(options.EndPoint, out IPEndPoint endPoint))
            {
                Console.WriteLine($"Could not parse endpoint {options.EndPoint}");
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
            ServerListener.Run(endPoint, null);

            IPEndPoint sslEndPoint = new IPEndPoint(endPoint.Address, endPoint.Port + 1);

            Console.WriteLine($"Running server on {sslEndPoint} (SSL)");
            ServerListener.Run(sslEndPoint, SslHelper.CreateSelfSignedCert());

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);

            return 1;
        }

        static int Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.HelpWriter = Console.Out;
            });

            Console.WriteLine($"Using {typeof(Socket).Assembly.FullName} from {typeof(Socket).Assembly.Location}");

            return parser.ParseArguments<ClientOptions, ServerOptions>(args).MapResult(
                (ClientOptions opts) => RunClient(opts),
                (ServerOptions opts) => RunServer(opts),
                _ => 1
            );
        }
    }
}
