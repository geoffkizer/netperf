//package io.netty.example.discard;

import io.netty.bootstrap.ServerBootstrap;

import io.netty.channel.ChannelFuture;
import io.netty.channel.ChannelInitializer;
import io.netty.channel.ChannelOption;
import io.netty.channel.ChannelPipeline;
import io.netty.channel.EventLoopGroup;
import io.netty.channel.nio.NioEventLoopGroup;
import io.netty.channel.socket.SocketChannel;
import io.netty.channel.socket.nio.NioServerSocketChannel;
import io.netty.handler.ssl.SslContext;
import io.netty.handler.ssl.SslContextBuilder;
import io.netty.handler.ssl.util.SelfSignedCertificate;
import io.netty.buffer.PooledByteBufAllocator;

/**
 * Discards any incoming data.
 */
public class Server {

    public Server() {
    }

    public ChannelFuture runListener(EventLoopGroup bossGroup, EventLoopGroup workerGroup, int port, SslContext sslCtx) throws Exception
    {
        ServerBootstrap b = new ServerBootstrap(); // (2)
        b.group(bossGroup, workerGroup)
            .channel(NioServerSocketChannel.class) // (3)
            .childHandler(new ChannelInitializer<SocketChannel>() { // (4)
                @Override
                public void initChannel(SocketChannel ch) throws Exception {
                    ChannelPipeline p = ch.pipeline();
                    if (sslCtx != null) {
                        p.addLast("tls", sslCtx.newHandler(ch.alloc()));
                    }
                    p.addLast(new ServerHandler());
                }
            })
            .option(ChannelOption.SO_BACKLOG, 128)          // (5)
            .childOption(ChannelOption.SO_KEEPALIVE, true) // (6)
            .childOption(ChannelOption.ALLOCATOR, new PooledByteBufAllocator(true));

        // Bind and start to accept incoming connections.
        ChannelFuture f = b.bind(port).sync(); // (7)

        return f;
    }

    public void run() throws Exception {
        // Some certificate stuff
        SelfSignedCertificate ssc = new SelfSignedCertificate();
        SslContext sslCtx = SslContextBuilder.forServer(ssc.certificate(), ssc.privateKey()).build();

        EventLoopGroup bossGroup = new NioEventLoopGroup(); // (1)
        EventLoopGroup workerGroup = new NioEventLoopGroup();
        try {
            ChannelFuture f1 = runListener(bossGroup, workerGroup, 5000, null);
            System.out.println("Listening on *:5000");

            ChannelFuture f2 = runListener(bossGroup, workerGroup, 5001, sslCtx);
            System.out.println("Listening on *:5001 (ssl)");

            System.out.println("Server running");

            // Wait until the server socket is closed.
            // In this example, this does not happen, but you can do that to gracefully
            // shut down your server.
            f1.channel().closeFuture().sync();
            f2.channel().closeFuture().sync();
        } finally {
            workerGroup.shutdownGracefully();
            bossGroup.shutdownGracefully();
        }
    }

    public static void main(String[] args) throws Exception {
        new Server().run();
    }
}