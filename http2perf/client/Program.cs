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
    private static int s_startupTime = 5;
    private static Version s_version = HttpVersion.Version11;

    private static void ProcessArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg == "2")
            {
                s_version = HttpVersion.Version20;
                Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT", "true");
            }
            else if (arg == "winhttp")
            {
                Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", "false");
            }
            else if (arg == "-?")
            {
                Console.WriteLine("Optional arguments:");
                Console.WriteLine("    2          Use HTTP2 (default is HTTP/1.1)");
                Console.WriteLine("    winhttp    Use WinHttpHandler (default is SocketsHttpHandler)");
            }
            else 
            {
                Console.WriteLine($"Unknown argument: {arg}");
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

    private static async Task RunWorker(WorkerState workerState)
    {
        var handler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        var invoker = new HttpMessageInvoker(handler);
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

        WorkerState workerState = new WorkerState();
        Task.Run(() => RunWorker(workerState));

        Console.WriteLine($"Waiting {s_startupTime} seconds for startup");

        Thread.Sleep(s_startupTime * 1000);

        int baseRequests = workerState.RequestsMade;
        DateTime baseTime = DateTime.Now;

        while (true)
        {
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            int startRequests = workerState.RequestsMade;

            Thread.Sleep(1000);

            gen0 = GC.CollectionCount(0) - gen0;
            gen1 = GC.CollectionCount(1) - gen1;
            gen2 = GC.CollectionCount(2) - gen2;

            int endRequests = workerState.RequestsMade;
            TimeSpan elapsed = DateTime.Now - baseTime;
            int totalRequests = endRequests - baseRequests;
            double rps = (totalRequests / elapsed.TotalSeconds);
            Console.WriteLine($"{elapsed}: {endRequests - startRequests}, average {rps:0.0} : {gen0} / {gen1} / {gen2}");
        }
    }
}
