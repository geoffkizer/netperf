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

        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Benchmarks scatter/gather server")
            {
                TreatUnmatchedTokensAsErrors = true,
            };

            var remoteCommand = new Command("server", "Runs the scatter gather server.");
            remoteCommand.AddOption(new Option<string>("--ip", () => DefaultIPAddress, "The IP to bind to."));
            remoteCommand.AddOption(new Option<int>("--port", () => DefaultPort, "The port to bind to."));
            remoteCommand.AddOption(new Option<int>("--size", () => DefaultContentSize, "Size of response content to send."));
            remoteCommand.Handler = CommandHandler.Create<string, int, int>((ip, port, size) =>
            {
                IPAddress address = IPAddress.Parse(ip);

                // TODO: handle op mode
                var server = new Server(new IPEndPoint(address, port), size);

                Console.WriteLine($"Server bound on {server.EndPoint}");
                Console.WriteLine($"Content-Length: {size}");

                server.Run();
            });

            rootCommand.AddCommand(remoteCommand);
            rootCommand.Invoke(args);
        }
    }
}
