using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using MyApp;

public class Program
{
    public static async Task Main()
    {
        Task t = EventLoop.Start(eventLoop =>
        {
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Register an event handler for a single-shot event
            eventLoop.On(clientSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 1234)),
                async () =>
                {
                    await clientSocket.SendAsync(new byte[] { 1, 2, 3 }, SocketFlags.None);

                    byte[] buffer = new byte[1024];
                    await clientSocket.ReceiveAsync(buffer, SocketFlags.None);

                    clientSocket.Close();
                });

            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 1234));
            listenSocket.Listen(1);

            // Register an event handler for a repeating event
            eventLoop.OnEvery(() => listenSocket.AcceptAsync(),
                async serverSocket =>
                {
                    byte[] buffer = new byte[1024];
                    await serverSocket.ReceiveAsync(buffer, SocketFlags.None);

                    await serverSocket.SendAsync(new byte[] { 1, 2, 3 }, SocketFlags.None);

                    serverSocket.Close();
                });

            // Shortcuts for timers
            eventLoop.After(500, () => { Console.WriteLine("500 milliseconds have passed"); });
            eventLoop.AfterEvery(5000, () => { Console.WriteLine("Invoke cleanup/scavenge logic here, every 5 seconds"); });

            // Example of deregistering an event handler
            int i = 0;
            EventRegistration eventRegistration = eventLoop.AfterEvery(1000, () =>
            {
                Console.WriteLine($"Iteration {i}"); 
                i++;

                // Stop after 100 iterations
                if (i == 100)   
                    eventRegistration.Dispose();
            });

            // Just run an arbitrary piece of async code on the event loop.
            // Equivalent to EventLoop.On(Task.CompletedTask, ...)
            eventLoop.Run(async () =>
            {
                Console.WriteLine("Hello");
                await Task.Delay(1000);
                Console.WriteLine("World");
            });
        });

        // Wait for event loop to complete
        await t;
    }
}
