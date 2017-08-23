# SocketPerfTest
This is a general purpose client/server app for measuring .NET core Socket and SslStream performance.

The app measures socket throughput by sending request and response messages of a specified size between client and server, over a specified number of connections.

## Usage

```
SocketPerfTest client -e 1.2.3.4:5000
```
Run the client against the specified IP address and port.

```
SocketPerfTest server -e 1.2.3.4:5000
```
Run the server on the specified IP address and port.

```
SocketPerfTest inproc
```
Run both client and server in a single process, over loopback.

## Additional options

```
--ssl
```
Use SslStream.  Default is raw sockets.

```
-s messageSize
``` 
Set the message size.  Default is 256 bytes.

```
-c connectionCount
```
Set the number of connections to establish.  Default is 256.  (Client only.)

See --help for additional options.


