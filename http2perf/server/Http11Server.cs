using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public sealed class Http11Server
{
    private Stream _stream;
    private ArrayBuffer _readBuffer;

    public Http11Server(Stream stream)
    {
        _stream = stream;
        _readBuffer = new ArrayBuffer(4096);
    }

    private const byte CR = (byte)'\r';
    private const byte LF = (byte)'\n';

    private static readonly ReadOnlyMemory<byte> s_headerTerminator = new ReadOnlyMemory<byte>(new byte[] { CR, LF, CR, LF });

    private const string s_responseString = "HTTP/1.1 200 OK\r\nContent-Length: 12\r\nServer: PerfServer\r\nDate: Wed, 21 Oct 2015 07:28:00 GMT\r\n\r\nHello World!";
    private static ReadOnlyMemory<byte> s_responseBytes = Encoding.ASCII.GetBytes(s_responseString);

    public async void Run()
    {
        try 
        {
            while (true)
            {
                // Read request

                while (true)
                {
                    int index = _readBuffer.ActiveSpan.IndexOf(s_headerTerminator.Span);
                    if (index != -1)
                    {
                        _readBuffer.Discard(index + s_headerTerminator.Length);
                        break;
                    }

                    // Read more data
                    _readBuffer.Compact();
                    if (_readBuffer.AvailableMemory.Length == 0)
                    {
                        throw new Exception("Request size exceeded");
                    }

                    int bytesRead = await _stream.ReadAsync(_readBuffer.AvailableMemory);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Client closed HTTP/1.1 connection");
                        return;
                    }

                    _readBuffer.Commit(bytesRead);
                }

                // Write response

                await _stream.WriteAsync(s_responseBytes);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"IOException on HTTP/1.1 connetion, terminating: {e.Message}");
        }
    }
}