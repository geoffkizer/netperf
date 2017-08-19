using System;
using System.IO;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal sealed class ServerHandler : BaseHandler
    {
        public ServerHandler(Stream stream, int messageSize)
            : base(stream, messageSize)
        {
        }

        public override async Task Run()
        {
            Trace("ServerHandler running");

            try
            {
                // Loop, receiving requests and sending responses
                while (true)
                {
                    if (!await ReceiveMessage())
                    {
                        Dispose();
                        return;
                    }

                    await SendMessage();
                }
            }
            catch (Exception e)
            {
                Trace($"Caught exception {e} in ServerHandler");
                Dispose();
                return;
            }
        }
    }
}
