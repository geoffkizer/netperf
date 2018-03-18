using System;
using System.IO;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal sealed class ServerHandler : BaseHandler
    {
        private byte[] _messageBuffer;

        public ServerHandler(BufferManager bufferManager, Stream stream)
            : base(bufferManager, stream)
        {
        }

        public override async Task Run()
        {
            Trace("ServerHandler running");

            try
            {
                int messageByteCount = 0;
                // Loop, receiving requests and sending responses
                while (true)
                {
                    int count = await _stream.ReadAsync(new Memory<byte>(_readBuffer));
                    if (count == 0)
                    {
                        // EOF when trying to receive from client.
                        // Shut down this handler.
                        Dispose();
                        return;
                    }

                    int offset = 0;
                    while (true)
                    {
                        int index = Array.IndexOf<byte>(_readBuffer, 0, offset, count);
                        if (index < 0)
                        {
                            // Consume all remaining bytes
                            messageByteCount += count;
                            break;
                        }

                        messageByteCount += index + 1;
                        if (_messageBuffer == null)
                        {
                            // First message received.
                            // Construct a response message of the same size, and send it
                            _messageBuffer = CreateMessageBuffer(messageByteCount);
                        }
                        else
                        {
                            // We expect the same size message from the client every time, so check this.
                            if (messageByteCount != _messageBuffer.Length)
                            {
                                Trace($"Expected message size {_messageBuffer.Length} but received {messageByteCount}");
                                Dispose();
                                return;
                            }
                        }

                        await _stream.WriteAsync(new Memory<byte>(_messageBuffer));

                        messageByteCount = 0;
                        offset += index + 1;
                        count -= index + 1;
                    }
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
