using System;
using System.Net;
using System.Threading;
using CommandLine;

namespace SocketPerfTest
{
    class Program
    {
        [Verb("clrthreads", HelpText = "Run server on CLR thread pool.")]
        private class ClrThreadsOptions
        {
        }

        [Verb("customthreads", HelpText = "Run server on custom thread pool.")]
        private class CustomThreadsOptions
        {
        }

        [Verb("threadbound", HelpText = "Run server on thread-bound IO threads.")]
        private class ThreadBoundOptions
        {
            [Option('b', "batchSize", Default = 1, HelpText = "Batch size to GetQueuedCompletionStatusEx")]
            public int BatchSize { get; set; }
        }

        static int RunClrThreads(ClrThreadsOptions options)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);

            Console.WriteLine($"Running server on {endPoint} (raw)");
            Console.WriteLine($"Running on CLR thread pool");
            ServerListener.Run(endPoint, "clrthreads", 0);

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);

            return 1;
        }

        static int RunCustomThreads(CustomThreadsOptions options)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);

            Console.WriteLine($"Running server on {endPoint} (raw)");
            Console.WriteLine($"Running on custom thread pool");
            ServerListener.Run(endPoint, "customthreads", 0);

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);

            return 1;
        }

        static int RunThreadBound(ThreadBoundOptions options)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);

            Console.WriteLine($"Running server on {endPoint} (raw)");
            Console.WriteLine($"Running on thread-bound IO threads with batch size = {options.BatchSize}");
            ServerListener.Run(endPoint, "threadbound", options.BatchSize);

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);

            return 1;
        }

        static void Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.HelpWriter = Console.Out;
            });

            parser.ParseArguments<ClrThreadsOptions, CustomThreadsOptions, ThreadBoundOptions>(args).MapResult(
                (ClrThreadsOptions opts) => RunClrThreads(opts),
                (CustomThreadsOptions opts) => RunCustomThreads(opts),
                (ThreadBoundOptions opts) => RunThreadBound(opts),
                _ => 1
            );
        }
    }
}
