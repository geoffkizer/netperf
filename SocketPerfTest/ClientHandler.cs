using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace SslStreamPerf
{
    internal sealed class ClientHandler : BaseHandler
    {
        private byte[] _messageBuffer;
        private int _requestCount;

        public ClientHandler(Stream stream, int messageSize)
            : base(stream)
        {
            _messageBuffer = CreateMessageBuffer(messageSize);
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
                    await _stream.WriteAsync(_messageBuffer, 0, _messageBuffer.Length);

                    int messageBytes = await ReceiveMessage();
                    if (messageBytes == 0)
                    {
                        // Server should never terminate the connection.
                        Console.WriteLine("ERROR: Server disconnected");
                        Environment.Exit(-1);
                    }
                    else if (messageBytes != _messageBuffer.Length)
                    {
                        // Wrong message size
                        Console.WriteLine($"ERROR: Expected {_messageBuffer.Length} bytes, received {messageBytes}");
                        Environment.Exit(-1);
                    }

                    _requestCount++;
                }
            }
            catch (IOException e)
            {
                Console.WriteLine($"ERROR: Caught IO exception {e} in ClientHandler");
                Environment.Exit(-1);
            }
        }
    }
}
