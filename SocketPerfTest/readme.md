# SocketPerfTest
This is a general purpose client/server app for measuring .NET core Socket and SslStream performance.

The app measures socket throughput by sending request and response messages of a specified size between client and server, over a specified number of connections.  The server will detect the size of messages sent from the client and send a response message of the same size.

## Running the server

```
SocketPerfTest server [-e 1.2.3.4:5000]
```
This will run a raw socket server on the specified IP/port and an SSL server on port+1.  Default IP/port is *:5000.

## Running the client

```
SocketPerfTest client -e 1.2.3.4:5000 [--ssl]
```
Run the client against the specified IP address and port.  Use SSL if `--ssl` is specified. 

Other client options:

```
-s messageSize
``` 
Set the message size.  Default is 256 bytes.

```
-c connectionCount
```
Set the number of connections to establish.  Default is 256.

Use `SocketPerfTest client --help` for additional options, including reporting options.

## Running in a single process

```
SocketPerfTest inproc
```
Run both client and server in a single process, over loopback.  Client options may be specified as well.

This mode is not recommended, but may be useful for quick and dirty results.




