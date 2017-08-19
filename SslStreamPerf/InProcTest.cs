using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal static class InProcTest
    {
        public static ClientHandler[] Start(int clientCount, int messageSize)
        {
            var clientHandlers = new ClientHandler[clientCount];

            for (int i = 0; i < clientCount; i++)
            {
                (var s1, var s2) = ProducerConsumerStream.Create();

                var clientHandler = new ClientHandler(s1, messageSize);
                Task.Run(() => clientHandler.Run());

                var serverHandler = new ServerHandler(s2, messageSize);
                Task.Run(() => serverHandler.Run());

                clientHandlers[i] = clientHandler;
            }

            return clientHandlers;
        }
    }
}
