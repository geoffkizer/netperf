using System;
using System.IO;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal sealed class ServerHandler : BaseHandler
    {
        private byte[] _messageBuffer;

        public ServerHandler(Stream stream)
            : base(stream)
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
                    int messageBytes = await ReceiveMessage();
                    if (messageBytes == 0)
                    {
                        // EOF when trying to receive from client.
                        // Shut down this handler.
                        Dispose();
                        return;
                    }

                    if (_messageBuffer == null)
                    {
                        // First message received.
                        // Construct a response message of the same size, and send it
                        _messageBuffer = CreateMessageBuffer(messageBytes);
                    }
                    else
                    {
                        // We expect the same size message from the client every time, so check this.
                        if (messageBytes != _messageBuffer.Length)
                        {
                            Trace($"Expected message size {_messageBuffer.Length} but received {messageBytes}");
                            Dispose();
                            return;
                        }
                    }

                    await _stream.WriteAsync(_messageBuffer, 0, _messageBuffer.Length);
                }
            }
            catch (IOException e)
            {
                // IO error when trying to receive from client.
                // Shut down this handler.
                Trace($"Caught IO exception {e} in ServerHandler");
                Dispose();
                return;
            }
        }
    }
}
