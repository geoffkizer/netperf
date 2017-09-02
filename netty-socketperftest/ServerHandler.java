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

    int _bytesRead = 0;
    byte[] _messageBuffer;

    private boolean tryReadMessage(ByteBuf readBuffer)
    {
        int offset = readBuffer.bytesBefore((byte)0);
        if (offset == -1)
        {
            _bytesRead += readBuffer.readableBytes();
            readBuffer.skipBytes(readBuffer.readableBytes());
            return false;
        }

        _bytesRead += offset + 1;
        readBuffer.skipBytes(offset + 1);
        return true;
    }

    private byte[] createMessageBuffer(int messageBytes)
    {
        byte[] messageBuffer = new byte[messageBytes];
        for (int i = 0; i < messageBytes - 1; i++)
        {
            messageBuffer[i] = (byte)0xFF;
        }

        messageBuffer[messageBytes - 1] = 0;
        return messageBuffer;
    }

    @Override
    public void channelRead(ChannelHandlerContext ctx, Object msg) throws Exception {
        ByteBuf buf = (ByteBuf)msg;

        // Extract messages from the buffer
        while (tryReadMessage(buf))
        {
            if (_messageBuffer == null)
            {
                // First message received.
                // Construct a response message of the same size, and send it
                _messageBuffer = createMessageBuffer(_bytesRead);
            }
            else
            {
                if (_bytesRead != _messageBuffer.length)
                {
            	    throw new Exception("Expected message size " + _messageBuffer.length + " but received " + _bytesRead);
                }
            }

            // Write the response
            ctx.write(wrappedBuffer(_messageBuffer));
            ctx.flush();

            _bytesRead = 0;
        }
        
        // Release the message
        buf.release();
    }

    @Override
    public void exceptionCaught(ChannelHandlerContext ctx, Throwable cause) { // (4)
        // Close the connection when an exception is raised.
//        cause.printStackTrace();
        ctx.close();
    }
}
