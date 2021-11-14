# SocketPerfTest2
This is a general purpose client/server app for measuring .NET Core Socket and SslStream performance.

The app measures socket throughput by sending request and response messages of a specified size between client and server, over a specified number of connections.

## Running the server

```
SocketPerfTest server [-e 1.2.3.4:5000] [--ssl] [-s responseSize]
```
Run the server on the specified IP/port, or 127.0.0.1:5000 if unspecified. Specify --ssl to use SSL.

Use `SocketPerfTest server --help` for additional options.

## Running the client

```
SocketPerfTest client [-e 1.2.3.4:5000] [--ssl] [-s requestSize] [-c connectionCount]
```
Run the client against the specified IP/port, or 127.0.0.1:5000 if unspecified. Specify --ssl to use SSL.

Use `SocketPerfTest client --help` for additional options, including reporting options.

