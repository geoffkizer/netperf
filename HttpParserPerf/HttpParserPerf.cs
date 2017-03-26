using CommandLine;
using System;
using System.Diagnostics;
//using System.Net.Http;
using System.Net.Http.Parser;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsoleApplication
{
    public class Program
    {
//        private static long _httpClientCounter = 0;
        private static int[] _queuedRequests;

        private const string _payload =
            @"{ ""data"": ""{'job_id':'c4bb6d130003','container_id':'ab7b85dcac72','status':'Success: process exited with code 0.'}"" }";

        private static Stopwatch _stopwatch = Stopwatch.StartNew();

        private static long _requests;
        private static long _ticks;

        private static Options _options;

        private class Options
        {
            [Option('u', "uri", Required = true)]
            public Uri Uri { get; set; }

            [Option('m', "method", Default = HttpMethod.Get)]
            public HttpMethod Method { get; set; }

            [Option('p', "parallel", Default = 512)]
            public int Parallel { get; set; }

            [Option('t', "threadingMode", Default = ThreadingMode.Task)]
            public ThreadingMode ThreadingMode { get; set; }

            [Option('c', "clients", Default = 1)]
            public int Clients { get; set; }

            [Option('e', "expectContinue")]
            public bool? ExpectContinue { get; set; }

            [Option('r', "requests", Default = long.MaxValue)]
            public long Requests { get; set; }

            [Option('s', "clientSelectionMode", Default = ClientSelectionMode.TaskRoundRobin)]
            public ClientSelectionMode ClientSelectionMode { get; set; }

            [Option("minQueue", Default = 0)]
            public int MinQueue { get; set; }

            [Option("maxQueue", Default = int.MaxValue)]
            public int MaxQueue { get; set; }

            [Option('v', "verbose", Default = false)]
            public bool Verbose { get; set; }
        }

        private enum HttpMethod
        {
            Get,
            Post
        }

        private enum ThreadingMode
        {
            Task,
            Thread
        }

        private enum ClientSelectionMode
        {
            TaskRoundRobin,
            TaskRandom,
            RequestRoundRobin,
            RequestRandom,
            RequestShortestQueue,
            RequestRandomNotLongestQueue,
            RequestRandomTolerance
        }

        public static int Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = Console.Out;
            });

            return parser.ParseArguments<Options>(args).MapResult(
                options => Run(options),
                _ => 1
            );
        }

        private static int Run(Options options)
        {
            _options = options;

            Console.WriteLine(
                $"{options.Method.ToString().ToUpperInvariant()} {options.Uri} with " +
                $"{options.Parallel} {options.ThreadingMode.ToString().ToLowerInvariant()}(s), " +
                $"{options.Clients} client(s), " +
                $"ClientSelectionMode={options.ClientSelectionMode.ToString()}, " +
                $"MinQueue={options.MinQueue}, MaxQueue={options.MaxQueue}, " +
                $"and ExpectContinue={options.ExpectContinue?.ToString() ?? "null"}" +
                "...");

            var writeResultsTask = WriteResults();

            RunTest();

            writeResultsTask.Wait();

            return 0;
        }

        private static byte[] s_requestMessage;

        // Since the point is to test parser performance,
        // we construct the request message up front and then send it directly on each request.
        private static void ConstructGetMessage()
        {
            Uri uri = _options.Uri;

            // I shouldn't include port if it's the default port, but punt for now.
            string message = $"GET {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Host}:{uri.Port}\r\n\r\n";
            s_requestMessage = Encoding.UTF8.GetBytes(message);
        }

        private static void RunTest()
        {
            _queuedRequests = new int[_options.Clients];

            if (_options.Method == HttpMethod.Get)
            {
                ConstructGetMessage();
            }
            else
            {
                throw new NotSupportedException();
            }

            var tasks = new Task[_options.Parallel];
            for (var i = 0; i < _options.Parallel; i++)
            {
                int clientId = -1;
                var task = ExecuteRequestsAsync(clientId);
                tasks[i] = task;
            }

            Task.WaitAll(tasks);
        }

        private static async Task ExecuteRequestAsync(BufferedStream bufferedStream, IHttpParserHandler handler)
        {
            // Send request
            await bufferedStream.WriteAsync(s_requestMessage, 0, s_requestMessage.Length, CancellationToken.None);
            await bufferedStream.FlushAsync(CancellationToken.None);

            // Parse response message
            HttpContentReadStream body = await HttpParser.ParseResponseAndGetBodyAsync(bufferedStream, handler, CancellationToken.None);

            await body.DrainAsync(CancellationToken.None);
            body.Dispose();
        }

        public static async Task<NetworkStream> ConnectAsync(string host, int port)
        {
            TcpClient client;

            // You would think TcpClient.Connect would just do this, but apparently not.
            // It works for IPv4 addresses but seems to barf on IPv6.
            // I need to explicitly invoke the constructor with AddressFamily = IPv6.
            // TODO: Does this mean that connecting by name will only work with IPv4
            // (since that's the default)?  If so, need to rework this logic
            // to resolve the IPAddress ourselves.  Yuck.
            // TODO: No cancellationToken on ConnectAsync?
            IPAddress ipAddress;
            if (IPAddress.TryParse(host, out ipAddress))
            {
                client = new TcpClient(ipAddress.AddressFamily);
                await client.ConnectAsync(ipAddress, port);
            }
            else
            {
                client = new TcpClient();
                await client.ConnectAsync(host, port);
            }

            client.NoDelay = true;

            NetworkStream networkStream = client.GetStream();
            return networkStream;
        }

        private static async Task ExecuteRequestsAsync(int clientId)
        {
            NetworkStream stream = await ConnectAsync(_options.Uri.Host, _options.Uri.Port);
            BufferedStream bufferedStream = new BufferedStream(stream);
            IHttpParserHandler handler = null;      // TODO

            long requestId;
            while ((requestId = Interlocked.Increment(ref _requests)) <= _options.Requests)
            {
                var start = _stopwatch.ElapsedTicks;
                await ExecuteRequestAsync(bufferedStream, handler);
                var end = _stopwatch.ElapsedTicks;

                Interlocked.Add(ref _ticks, end - start);
            }
            Interlocked.Decrement(ref _requests);
        }

        private static async Task WriteResults()
        {
            var lastRequests = (long)0;
            var lastTicks = (long)0;
            var lastElapsed = TimeSpan.Zero;

            do
            {
                await Task.Delay(TimeSpan.FromSeconds(1));

                var requests = _requests;
                var currentRequests = requests - lastRequests;
                lastRequests = requests;

                var ticks = _ticks;
                var currentTicks = ticks - lastTicks;
                lastTicks = ticks;

                var elapsed = _stopwatch.Elapsed;
                var currentElapsed = elapsed - lastElapsed;
                lastElapsed = elapsed;

                WriteResult(requests, ticks, elapsed, currentRequests, currentTicks, currentElapsed);
            }
            while (Interlocked.Read(ref _requests) < _options.Requests);
        }

        private static void WriteResult(long totalRequests, long totalTicks, TimeSpan totalElapsed,
            long currentRequests, long currentTicks, TimeSpan currentElapsed)
        {
            var totalMs = ((double)totalTicks / Stopwatch.Frequency) * 1000;
            var currentMs = ((double)currentTicks / Stopwatch.Frequency) * 1000;

            Console.WriteLine(
                $"{DateTime.UtcNow.ToString("o")}\tTot Req\t{totalRequests}" +
                $"\tCur RPS\t{Math.Round(currentRequests / currentElapsed.TotalSeconds)}" +
                $"\tCur Lat\t{Math.Round(currentMs / currentRequests, 2)}ms" +
                $"\tAvg RPS\t{Math.Round(totalRequests / totalElapsed.TotalSeconds)}" +
                $"\tAvg Lat\t{Math.Round(totalMs / totalRequests, 2)}ms" +
                $"\tReq\t{String.Join(" ", _queuedRequests)}"
            );
        }

        public static class ConcurrentRandom
        {
            private static Random _global = new Random();

            [ThreadStatic]
            private static Random _local;

            public static int Next()
            {
                Random inst = _local;
                if (inst == null)
                {
                    int seed;
                    lock (_global) seed = _global.Next();
                    _local = inst = new Random(seed);
                }
                return inst.Next();
            }
        }
    }
}
