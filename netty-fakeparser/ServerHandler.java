//package io.netty.example.discard;

import io.netty.buffer.ByteBuf;

import io.netty.channel.ChannelHandlerContext;
import io.netty.channel.ChannelInboundHandlerAdapter;

import static java.nio.charset.StandardCharsets.*;
import static io.netty.buffer.Unpooled.*;

/**
 * Handles a server-side channel.
 */
public class ServerHandler extends ChannelInboundHandlerAdapter { // (1)

//    static final int s_expectedReadSize = 848;
    static final byte[] s_responseMessage = (
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
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n")
            .getBytes(UTF_8);

//    int _bytesRead = 0;
    int requestCount = 0;
    ByteBuf currentBuf = null;

    private final boolean parseHttpRequest(ByteBuf buf)
    {
        while (true)
        {
            int offset = buf.bytesBefore((byte)'\r');
            if (offset == -1)
            {
                buf.skipBytes(buf.readableBytes());
                return false;
            }

            buf.skipBytes(offset + 1);
            if (buf.readableBytes() < 3)
            {
                return false;
            }

            if ((buf.readByte() == (byte)'\n') &&
                (buf.readByte() == (byte)'\r') &&
                (buf.readByte() == (byte)'\n'))
            {
//                buf.discardReadBytes();
                return true;
            }
        }
    }

    @Override
    public void channelRead(ChannelHandlerContext ctx, Object msg) { // (2)
        // (Try to) validate message length
        ByteBuf buf = (ByteBuf)msg;
        
//        System.out.println("Read bytes: " + buf.readableBytes());

        if (currentBuf != null)
        {
            // Combine with leftovers from previous
//            System.out.println("Previous bytes: " + currentBuf.readableBytes());
            buf = wrappedBuffer(currentBuf, buf);
        }

        while (true)
        {
            while (requestCount < 16)
            {
                if (parseHttpRequest(buf))
                {
//                    System.out.println("Parsed one http request, bytes remaining = " + buf.readableBytes());
                    requestCount++;
                }
                else
                {
                    // need more data
                    if (buf.readableBytes() > 0)
                    {
                        currentBuf = buf;
                    }
                    else
                    {
                        buf.release();
                        currentBuf = null;
                    }

                    return;
                }
            }

            // Write the response
            ctx.write(wrappedBuffer(s_responseMessage)); // (1)
            ctx.flush(); // (2)
        }
    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) { // (4)
        // Close the connection when an exception is raised.
        cause.printStackTrace();
        ctx.close();
    }
}
