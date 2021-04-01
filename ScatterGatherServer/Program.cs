using System;
using System.Net;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace ScatterGatherServer
{
    class Program
    {
        const string DefaultIPAddress = "127.0.0.1";
        const int DefaultPort = 8000;
        const int DefaultContentSize = 1;
        const ServerMode DefaultServerMode = ServerMode.SendMultiple;

        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Benchmarks scatter/gather server");

            rootCommand.AddOption(new Option<string>("--ip", () => DefaultIPAddress, "The IP to bind to."));
            rootCommand.AddOption(new Option<int>("--port", () => DefaultPort, "The port to bind to."));
            rootCommand.AddOption(new Option<int>("--size", () => DefaultContentSize, "Size of response content to send."));
            rootCommand.AddOption(new Option<ServerMode>("--mode", () => DefaultServerMode, "How the server does sends for the response."));

            rootCommand.Handler = CommandHandler.Create<string, int, int, ServerMode>((ip, port, size, mode) =>
            {
                var server = new Server(new IPEndPoint(IPAddress.Parse(ip), port), size, mode);

                Console.WriteLine($"Server bound on {server.EndPoint}");
                Console.WriteLine($"Server send mode: {mode}");
                Console.WriteLine($"Content-Length: {size}");

                server.Run();
            });

            rootCommand.Invoke(args);
        }
    }
}
