using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

class Test
{
    private static int s_concurrencyLevel = 1;
    private static int s_startupTime = 5;
    private static Version s_version = HttpVersion.Version11;

    private static void ProcessArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "2")
            {
                s_version = HttpVersion.Version20;
                Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "true");
            }
            else if (args[i] == "winhttp")
            {
                Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", "false");
            }
            else if (args[i] == "-c")
            {
                i++;
                if (i == args.Length)
                {
                    Console.WriteLine($"Missing value for -c argument");
                    Environment.Exit(-1);
                }

                if (!int.TryParse(args[i], out s_concurrencyLevel))
                {
                    Console.WriteLine($"Could not parse value for -c argument");
                    Environment.Exit(-1);
                }
            }
            else if (args[i] == "-?")
            {
                Console.WriteLine("Optional arguments:");
                Console.WriteLine("    -c <n>     Concurrency level (default is 1)");
                Console.WriteLine("    2          Use HTTP2 (default is HTTP/1.1)");
                Console.WriteLine("    winhttp    Use WinHttpHandler (default is SocketsHttpHandler)");
                Environment.Exit(0);
            }
            else 
            {
                Console.WriteLine($"Unknown argument: {args[i]}");
                Environment.Exit(-1);
            }
        }
    }

    private sealed class WorkerState
    {
        public int RequestsMade;

        public WorkerState()
        {
            RequestsMade = 0;
        }
    }

    private static async Task RunWorker(HttpMessageInvoker invoker, WorkerState workerState)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "https://localhost:5001/");
        message.Version = s_version;

        Memory<byte> readBuffer = new byte[4096];
        try
        {
            while (true)
            {
                using (HttpResponseMessage r = await invoker.SendAsync(message, default(CancellationToken)))
                using (Stream s = await r.Content.ReadAsStreamAsync())
                {
                    if (r.StatusCode != HttpStatusCode.OK)
                    {
                        throw new Exception($"Unexpected response status code: {r.StatusCode}");
                    }

                    int totalBytes = 0;
                    while (true)
                    {
                        int bytesRead = await s.ReadAsync(readBuffer);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        totalBytes += bytesRead;
                    }

                    if (totalBytes != 12)
                    {
                        throw new Exception($"Unexpected response body length: {totalBytes}");
                    }
                }

                workerState.RequestsMade++;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Caught exception: {e}");
            return;
        }
    }

    public static void Main(string[] args)
    {
        ProcessArgs(args);

        Console.WriteLine($"Protocol version is {s_version}");
        Console.WriteLine($"Concurrency level is {s_concurrencyLevel}");

        var handler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        var invoker = new HttpMessageInvoker(handler);

        WorkerState[] workerStates = new WorkerState[s_concurrencyLevel];
        for (int i = 0; i < s_concurrencyLevel; i++)
        {
            var workerState = new WorkerState();
            workerStates[i] = workerState;
            Task.Run(() => RunWorker(invoker, workerState));
        }

        Console.WriteLine($"Waiting {s_startupTime} seconds for startup");

        Thread.Sleep(s_startupTime * 1000);

        int baseRequests = workerStates.Sum(w => w.RequestsMade);
        DateTime baseTime = DateTime.Now;

        while (true)
        {
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            int startRequests = workerStates.Sum(w => w.RequestsMade);

            Thread.Sleep(1000);

            gen0 = GC.CollectionCount(0) - gen0;
            gen1 = GC.CollectionCount(1) - gen1;
            gen2 = GC.CollectionCount(2) - gen2;

            int endRequests = workerStates.Sum(w => w.RequestsMade);
            TimeSpan elapsed = DateTime.Now - baseTime;
            int totalRequests = endRequests - baseRequests;
            double rps = (totalRequests / elapsed.TotalSeconds);
            Console.WriteLine($"{elapsed}: {endRequests - startRequests}, average {rps:0.0} : {gen0} / {gen1} / {gen2}");
        }
    }
}
