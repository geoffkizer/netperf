using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace SslStreamPerf
{
    internal sealed class ClientHandler : BaseHandler
    {
        private int _requestCount;

        public ClientHandler(Stream stream, int messageSize)
            : base(stream, messageSize)
        {
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public override async Task Run()
        {
            Trace("ClientHandler running");

            try
            {
                // Loop, sending requests and receiving responses
                while (true)
                {
                    await SendMessage();

                    if (!await ReceiveMessage())
                    {
                        Dispose();
                        return;
                    }

                    _requestCount++;
                }
            }
            catch (IOException e)
            {
                Trace($"Caught IO exception {e} in ClientHandler");
                Dispose();
                return;
            }
        }
    }
}
