using System;
using System.Net;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace ScatterGatherServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Benchmarks scatter/gather server")
            {
                TreatUnmatchedTokensAsErrors = true,
            };

            var remoteCommand = new Command("server", "Runs the scatter gather server.");
            remoteCommand.AddOption(new Option<string>("--ip", "The IP to bind to."));
            remoteCommand.AddOption(new Option<int>("--port", "The port to bind to."));
            remoteCommand.Handler = CommandHandler.Create<string, int?>((ip, port) =>
            {
                IPAddress address = ip is null ? IPAddress.Any : IPAddress.Parse(ip);
                port ??= 8000;

                // TODO: handle content size and op mode
                var server = new Server(new IPEndPoint(address, port.Value), 10);

                Console.WriteLine($"Server bound on {server.EndPoint}");

                server.Run();
            });

            rootCommand.AddCommand(remoteCommand);
            rootCommand.Invoke(args);
        }
    }
}
