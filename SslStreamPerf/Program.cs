using CommandLine;
using System;
using System.Threading;

namespace SslStreamPerf
{
    class Program
    {
        private class Options
        {
            [Option('c', "clients", Default = 16)]
            public int Clients { get; set; }

            [Option('m', "messageSize", Default = 2048)]
            public int MessageSize { get; set; }

            [Option('w', "warmupTime", Default = 5)]            // Seconds
            public int WarmupTime { get; set; }

            [Option('i', "reportingInterval", Default = 3)]     // Seconds
            public int ReportingInterval { get; set; }

            [Option('l', "useLoopback", Default = false)]     // Seconds
            public bool UseLoopback { get; set; }
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

        static void ShowResults(Options options, ClientHandler[] clientHandlers)
        {
            Console.WriteLine($"Test running with {options.Clients} clients and MessageSize={options.MessageSize}");

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
            while (true)
            {
                Thread.Sleep(options.ReportingInterval * 1000);

                elapsed += options.ReportingInterval;
                int currentCount = GetCurrentRequestCount(clientHandlers);

                double currentRPS = (currentCount - previousCount) / (double)options.ReportingInterval;
                double averageRPS = (currentCount - countAfterWarmup) / (double)elapsed;

                Console.WriteLine($"Elapsed time: {TimeSpan.FromSeconds(elapsed)}    Current RPS: {currentRPS:0.0}    Average RPS: {averageRPS:0.0}");

                previousCount = currentCount;
            }
        }

        static int Run(Options options)
        {
            Console.WriteLine("Starting up...");

            var cert = SslHelper.LoadCert();

            ClientHandler[] clientHandlers;
            if (options.UseLoopback)
            {
                Console.WriteLine("Running in-process over loopback");
                clientHandlers = LoopbackTest.Start(cert, options.Clients, options.MessageSize);
            }
            else
            {
                Console.WriteLine("Running in-process over in-memory stream");
                clientHandlers = InProcTest.Start(cert, options.Clients, options.MessageSize);
            }

            ShowResults(options, clientHandlers);

            return 1;
        }

        static int Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.HelpWriter = Console.Out;
            });

            return parser.ParseArguments<Options>(args).MapResult(
                options => Run(options),
                _ => 1
            );
        }
    }
}
