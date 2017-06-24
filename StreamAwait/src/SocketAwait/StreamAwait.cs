using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketAwait
{
    public class StreamAwait
    {
        public static Socket s_listenSocket;

        public const bool s_trace = false;
        public const bool s_useSsl = false;

        public static readonly byte[] s_responseMessage = Encoding.UTF8.GetBytes(
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n");

//        public const int s_expectedReadSize = 848;

        struct HttpRequestParseState
        {
            const byte CR = (byte)'\r';
            const byte LF = (byte)'\n';

            enum State
            {
                None = 0,
                SawCR = 1,
                SawCRLF = 2,
                SawCRLFCR = 3
            }

            private State _state;

            // Returns 0 if more data needed, and updates state
            // Returns positive int to indicate bytes consumed, and sets state to None
            public int TryParse(byte[] buffer, int offset, int length)
            {
                int i = offset;
                int end = offset + length;

                // Dispatch to appropriate place in state machine
                switch (_state)
                {
                    case State.None:
                        while (i < end)
                        {
                            if (buffer[i++] == CR)
                                goto case State.SawCR;
                        }

                        _state = State.None;
                        return 0;

                    case State.SawCR:
                        if (i == end)
                        {
                            _state = State.SawCR;
                            return 0;
                        }

                        if (buffer[i++] == LF)
                            goto case State.SawCRLF;
                        else
                            goto case State.None;

                    case State.SawCRLF:
                        if (i == end)
                        {
                            _state = State.SawCRLF;
                            return 0;
                        }

                        if (buffer[i++] == CR)
                            goto case State.SawCRLFCR;
                        else
                            goto case State.None;

                    case State.SawCRLFCR:
                        if (i == end)
                        {
                            _state = State.SawCRLFCR;
                            return 0;
                        }

                        if (buffer[i++] == LF)
                        {
                            // Successfully found CRLFCRLF
                            // Return count of bytes consumed
                            _state = State.None;
                            return i - offset;
                        }
                        else
                            goto case State.None;

                    default:
                        throw new InvalidOperationException("invalid parse state");
                }
            }
        }

        sealed class Connection
        {
            private const int BufferSize = 4096;

            private Stream _stream;
            private byte[] _readBuffer = new byte[BufferSize];


            public Connection(Stream stream)
            {
                _stream = stream;
            }

            public async void Run()
            {
                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Loop, receiving requests and sending responses
                int readOffset = 0;
                int readLength = 0;
                while (true)
                {
                    // Receive 16 requests
                    var parseState = new HttpRequestParseState();
                    int requestCount = 0;
                    while (requestCount < 16)
                    {
                        int bytesParsed = parseState.TryParse(_readBuffer, readOffset, readLength);
                        if (bytesParsed == 0)
                        {
                            // Need to do another read
                            readOffset = 0;
                            try
                            {
                                readLength = await _stream.ReadAsync(_readBuffer, 0, BufferSize);
                            }
                            catch (IOException)
                            {
                                _stream.Dispose();
                                return;
                            }

                            if (readLength == 0)
                            {
                                if (s_trace)
                                {
                                    Console.WriteLine("Connection closed by client");
                                }

                                _stream.Dispose();
                                return;
                            }

                            if (s_trace)
                            {
                                Console.WriteLine("Read complete, bytesRead = {0}", readLength);
                            }
                        }
                        else
                        {
                            // Processed one request
                            readOffset += bytesParsed;
                            readLength -= bytesParsed;
                            requestCount++;
                        }
                    }

                    // Send 16 responses (hardcoded)
                    await _stream.WriteAsync(s_responseMessage, 0, s_responseMessage.Length);

                    if (s_trace)
                    {
                        Console.WriteLine("Write complete");
                    }
                }
            }
        }

        private static void HandleConnection(Socket s)
        {
            s.NoDelay = true;
            Stream stream = new NetworkStream(s);

            // TODO: SSL

            var c = new Connection(stream);
            c.Run();
        }

        private static void AcceptConnections()
        {
            while (true)
            {
                Socket s = s_listenSocket.Accept();
                Task.Run(() => HandleConnection(s));
            }
        }

        private static void Start()
        {
            s_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s_listenSocket.Bind(new IPEndPoint(IPAddress.Any, 5000));
            s_listenSocket.Listen(1000);

            Task.Run(() => AcceptConnections());
        }

        public static void Main(string[] args)
        {
            Start();

            Console.WriteLine("Server Running");
            Console.ReadLine();
        }
    }
}

