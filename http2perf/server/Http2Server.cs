using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using System.Buffers.Binary;

public sealed class Http2Server
{
    private Stream _stream;
    private ArrayBuffer _readBuffer;

    public Http2Server(Stream stream)
    {
        _stream = stream;
        _readBuffer = new ArrayBuffer(4096);
    }

    private static async ValueTask<int> ReadAtLeastAsync(Stream stream, Memory<byte> buffer, int minReadBytes)
    {
        Debug.Assert(buffer.Length >= minReadBytes);

        int totalBytesRead = 0;
        while (totalBytesRead < minReadBytes)
        {
            int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new Exception("Unexpected EOF encountered");
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    private async Task EnsureIncomingBytesAsync(int minReadBytes)
    {
        if (_readBuffer.ActiveSpan.Length >= minReadBytes)
        {
            return;
        }

        // Need to read more

        _readBuffer.Compact();

        int bytesNeeded = minReadBytes - _readBuffer.ActiveSpan.Length;
        if (bytesNeeded > _readBuffer.AvailableSpan.Length)
        {
            throw new Exception($"Buffer size exceeded: minReadBytes = {minReadBytes}");
        }

        int bytesRead = await ReadAtLeastAsync(_stream, _readBuffer.AvailableMemory, bytesNeeded).ConfigureAwait(false);
        _readBuffer.Commit(bytesRead);
    }

    private const int s_FrameHeaderSize = 9;

    private async Task<(int streamId, byte frameType)> ReadFrameAsync()
    {
        // Read frame header
        await EnsureIncomingBytesAsync(s_FrameHeaderSize).ConfigureAwait(false);

        // Read the frame header.
        // For simplicity, assume frame size is <64K.
        ushort frameSize = BinaryPrimitives.ReadUInt16BigEndian(_readBuffer.ActiveSpan.Slice(1));
        byte frameType = _readBuffer.ActiveSpan[3];
        int streamId = BinaryPrimitives.ReadInt32BigEndian(_readBuffer.ActiveSpan.Slice(5));

        await EnsureIncomingBytesAsync(s_FrameHeaderSize + frameSize).ConfigureAwait(false);

        // Discard the frame now. We don't care about its actual contents.
        _readBuffer.Discard(s_FrameHeaderSize + frameSize);

        return (streamId, frameType);
    }

    private static readonly Memory<byte> s_settingsFrame = new byte[] 
    {
        0, 0, 0,        // Length = 0
        0x04,           // Type == SETTINGS
        0,              // Flags = 0
        0, 0, 0, 0      // StreamId = 0
    };

    private const string s_contentLengthValue = "12";
    private const string s_serverValue = "PerfServer";
    private const string s_dateValue = "Wed, 21 Oct 2015 07:28:00 GMT";
    private const string s_responseBody = "Hello World!";

    private static readonly Memory<byte> s_ResponseFrame;
    private static readonly int s_headerFrameStart;
    private static readonly int s_dataFrameStart;

    static Http2Server()
    {
        int i = 0;
        byte[] buffer = new byte[1024];

        // HEADERS frame
        s_headerFrameStart = i;

        // Frame header
        // Length, calculated later
        buffer[i++] = 0;
        buffer[i++] = 0;
        buffer[i++] = 0;

        // Type = HEADERS
        buffer[i++] = 0x01;

        // Flags = END_HEADERS
        buffer[i++] = 0x04;

        // StreamId, filled in for each request
        buffer[i++] = 0;
        buffer[i++] = 0;
        buffer[i++] = 0;
        buffer[i++] = 0;

        // Frame contents
        buffer[i++] = 0x88;   // :status: 200

        // "Content-Length" header, encoding = 28
        buffer[i++] = 0x0F;
        buffer[i++] = (byte)(28 - 0x0F);

        buffer[i++] = (byte)s_contentLengthValue.Length;
        i += Encoding.ASCII.GetBytes(s_contentLengthValue, new Span<byte>(buffer).Slice(i));

        // "Server" header, encoding = 54
        buffer[i++] = 0x0F;
        buffer[i++] = (byte)(54 - 0x0F);

        buffer[i++] = (byte)s_serverValue.Length;
        i += Encoding.ASCII.GetBytes(s_serverValue, new Span<byte>(buffer).Slice(i));

        // "Server" header, encoding = 33
        buffer[i++] = 0x0F;
        buffer[i++] = (byte)(33 - 0x0F);

        buffer[i++] = (byte)s_dateValue.Length;
        i += Encoding.ASCII.GetBytes(s_dateValue, new Span<byte>(buffer).Slice(i));

        // Fill in length in frame header
        int headerFrameLength = i - (s_headerFrameStart + 9);
        BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(buffer).Slice(s_headerFrameStart + 1), (ushort)headerFrameLength);

        // DATA frame
        s_dataFrameStart = i;

        // Frame header
        // Length, calculated later
        buffer[i++] = 0;
        buffer[i++] = 0;
        buffer[i++] = 0;

        // Type = DATA
        buffer[i++] = 0x00;

        // Flags = END_STREAM
        buffer[i++] = 0x01;

        // StreamId, filled in for each request
        buffer[i++] = 0;
        buffer[i++] = 0;
        buffer[i++] = 0;
        buffer[i++] = 0;

        // Frame contents
        i += Encoding.ASCII.GetBytes(s_responseBody, new Span<byte>(buffer).Slice(i));

        // Fill in length in frame header
        int dataFrameLength = i - (s_dataFrameStart + 9);
        BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(buffer).Slice(s_dataFrameStart + 1), (ushort)dataFrameLength);

        s_ResponseFrame =  new Memory<byte>(buffer).Slice(0, i);
    } 


    public async void Run()
    {
        byte[] responseFrame = new byte[s_ResponseFrame.Length];
        s_ResponseFrame.CopyTo(responseFrame);

        try 
        {
            // Read 24 byte connection preface
            await EnsureIncomingBytesAsync(24).ConfigureAwait(false);
            _readBuffer.Discard(24);

            // Send SETTINGS frame
            await _stream.WriteAsync(s_settingsFrame);

            while (true)
            {
                // Read request

                int streamId;
                while (true)
                {
                    byte frameType;
                    (streamId, frameType) = await ReadFrameAsync().ConfigureAwait(false);
                    if (frameType == 1)     // HEADERS
                    {
                        break;
                    }
                    else if (frameType == 4)    // SETTINGS
                    { 
                        continue;
                    }
                    else if (frameType == 8)    // WINDOW_UPDATE
                    {
                        continue;
                    }
                    else
                    {
                        throw new Exception($"Unexpected frame type {frameType:X2}");
                    }
                }

                // Write response

                // First, write the stream ID
                BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(responseFrame).Slice(s_headerFrameStart + 5), streamId);
                BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(responseFrame).Slice(s_dataFrameStart + 5), streamId);

                await _stream.WriteAsync(responseFrame);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"IOException on HTTP/1.1 connetion, terminating: {e.Message}");
        }
    }
}