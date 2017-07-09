// NativeIOCP.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"

#define RESPONSE "HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n"
#define PIPELINED_RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE

const char s_responseMessage[] = PIPELINED_RESPONSE;
const int s_responseMessageLength = sizeof(s_responseMessage) - 1; // exclude trailing null

const int s_expectedReadSize = 2624;

// Cmd line arguments
bool s_trace = false;
bool s_syncCompletions = true;
int s_acceptCount = 1;

// Inherit from OVERLAPPED so we can cast to/from

template <class T> 
class OverlappedHelper : public OVERLAPPED
{
public:
	typedef void (T::* OverlappedCallback)(DWORD dwErrorCode, DWORD dwNumberOfBytesTransfered);

private:
	T* _target;
	OverlappedCallback _callback;

public:
	OverlappedHelper(T* target, OverlappedCallback callback) :
		_target(target),
		_callback(callback)
	{
	}

	static void BindSocket(SOCKET socket)
	{
		int err = BindIoCompletionCallback((HANDLE)socket, &CompletionCallback, 0);
		if (err == 0)
		{
			printf("BindIoCompletionCallback of socket failed with error: %u\n", GetLastError());
			exit(-1);
		}
	}

private:
	static void CALLBACK CompletionCallback(
		DWORD dwErrorCode,
		DWORD dwNumberOfBytesTransfered,
		LPOVERLAPPED lpOverlapped)
	{
		OverlappedHelper * helper = static_cast<OverlappedHelper*>(lpOverlapped);
		((helper->_target)->*(helper->_callback))(dwErrorCode, dwNumberOfBytesTransfered);
	}
};


class Connection 
{
private:
	SOCKET _socket;
	BOOL _isSsl;

	OverlappedHelper<Connection> _readHelper;
	OverlappedHelper<Connection> _writeHelper;

	BYTE _readBuffer[4096];
//	BYTE _writeBuffer[4096];

public:
	Connection(SOCKET s, BOOL isSsl) :
		_socket(s),
		_isSsl(isSsl),
		_readHelper(this, &Connection::OnRead),
		_writeHelper(this, &Connection::OnWrite)
	{
	}

	void Run()
	{
		if (s_trace)
		{
			printf("Connection accepted\n");
		}

		OverlappedHelper<Connection>::BindSocket(_socket);

		int err;
		if (s_syncCompletions)
		{
			err = SetFileCompletionNotificationModes((HANDLE)_socket, FILE_SKIP_COMPLETION_PORT_ON_SUCCESS | FILE_SKIP_SET_EVENT_ON_HANDLE);
			if (err == 0)
			{
				printf("SetFileCompletionNotificationModes of accepted socket failed with error: %u\n", GetLastError());
				exit(-1);
			}
		}

		BOOL nodelay = true;
		err = setsockopt(_socket, IPPROTO_TCP, TCP_NODELAY, (const char *)&nodelay, sizeof(BOOL));
		if (err != 0)
		{ 
			printf("setsockopt(TCP_NODELAY) failed with error: %u\n", WSAGetLastError());
			exit(-1);
		}

		DoRead();
	}

private:
	void DoRead()
	{
		WSABUF wbuf;
		wbuf.buf = (CHAR*)&_readBuffer;
		wbuf.len = 4096;
		DWORD bytesReceived;
		DWORD flags = 0;
		int err = WSARecv(_socket, &wbuf, 1, &bytesReceived, &flags, &_readHelper, NULL);
		if (err == 0)
		{
			// Synchronous completion
			if (s_syncCompletions)
				OnRead(0, bytesReceived);
		}
		else
		{
			int sockError = WSAGetLastError();
			if (sockError != WSA_IO_PENDING)
			{
				printf("WSARecv failed synchronously, error code = %u", sockError);
				exit(-1);
			}
		}
	}

	void OnRead(DWORD dwErrorCode, DWORD bytesRead)
	{
		if (dwErrorCode != 0)
		{
			// Just assume this is a connection reset, and stop processing the connection
			if (s_trace)
			{
				printf("Socket I/O failed, error code = %u\n", dwErrorCode);
			}

			closesocket(_socket);
			delete this;
			return;
		}

		if (bytesRead == 0)
		{
			if (s_trace)
			{
				printf("Connection closed by client\n");
			}

			closesocket(_socket);
			delete this;
			return;
		}

		if (s_trace)
		{
			printf("Read complete, bytesRead = %u\n", bytesRead);
		}

		if (bytesRead != s_expectedReadSize)
		{
			printf("Unexpected read size, bytesRead = %u", bytesRead);
			exit(-1);
		}

		// CONSIDER: May need to loop on read, probably isn't necessary for benchmarking

		WSABUF wbuf;
		wbuf.buf = (CHAR*)s_responseMessage;
		wbuf.len = s_responseMessageLength;
		DWORD bytes;
		int err = WSASend(_socket, &wbuf, 1, &bytes, 0, &_writeHelper, NULL);
		if (err == 0)
		{
			// Synchronous completion
			if (s_syncCompletions)
				OnWrite(0, bytes);
		}
		else
		{
			int sockError = WSAGetLastError();
			if (sockError != WSA_IO_PENDING)
			{
				printf("WSASend failed synchronously, error code = %u", sockError);
				exit(-1);
			}
		}
	}

	void OnWrite(DWORD dwErrorCode, DWORD bytesWritten)
	{
		if (dwErrorCode != 0)
		{
			// Just assume this is a connection reset, and stop processing the connection
			if (s_trace)
			{
				printf("Socket I/O failed, error code = %u\n", dwErrorCode);
			}

			closesocket(_socket);
			delete this;
			return;
		}

		if (s_trace)
		{
			printf("Write complete, bytesRead = %u\n", bytesWritten);
		}

		if (bytesWritten != s_responseMessageLength)
		{
			printf("Unexpected write size, bytesWritten = %u", bytesWritten);
			exit(-1);
		}

		DoRead();
	}
};

class ListenSocket
{
public:
	SOCKET _listenSocket;
	SOCKET _acceptSocket;
	BOOL _isSsl;
	OverlappedHelper<ListenSocket> _helper;

	ListenSocket(int port, BOOL isSsl) :
		_isSsl(isSsl),
		_helper(this, &ListenSocket::OnAccept),
		_acceptSocket(NULL)
	{
		// Create a listening socket
		_listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		if (_listenSocket == INVALID_SOCKET)
		{
			printf("Create of ListenSocket socket failed with error: %u\n", WSAGetLastError());
			exit(-1);
		}

		// Bind to the thread pool IOCP
		OverlappedHelper<ListenSocket>::BindSocket(_listenSocket);

		// Bind to IP/Port
		sockaddr_in sa;
		sa.sin_family = AF_INET;
		sa.sin_addr.S_un.S_addr = INADDR_ANY;
		sa.sin_port = htons(port);
		if (bind(_listenSocket, (SOCKADDR *)&sa, sizeof(sa)) == SOCKET_ERROR)
		{
			printf("bind failed with error: %u\n", WSAGetLastError());
			exit(-1);
		}

		// Listen
		int err = listen(_listenSocket, SOMAXCONN);
		if (err == SOCKET_ERROR)
		{
			printf("listen failed with error: %u\n", WSAGetLastError());
			exit(-1);
		}

		printf("Listening on port %d, ssl = %s\n", port, isSsl ? "true" : "false");
	}

	void Start()
	{
		DoAccept();
	}

private:
	void DoAccept()
	{
		if (_acceptSocket != NULL)
		{
			printf("_acceptSocket is not NULL???\n");
			exit(-1);
		}

		_acceptSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		if (_acceptSocket == INVALID_SOCKET)
		{
			printf("Create accept socket failed with error: %u\n", WSAGetLastError());
			exit(-1);
		}

//		memset(ToOverlapped(), 0, sizeof(OVERLAPPED));

		const int addressSize = sizeof(sockaddr_in6) + 16;
		BYTE outBuffer[addressSize * 2];
		BOOL success = AcceptEx(_listenSocket, _acceptSocket, &outBuffer, 0, addressSize, addressSize, NULL, &_helper);
		if (success == FALSE)
		{
			int error = WSAGetLastError();
			if (error != ERROR_IO_PENDING)
			{
				printf("AcceptEx failed with error: %u\n", WSAGetLastError());
				exit(-1);
			}
		}

		if (success == TRUE)
		{
			printf("Unexpected sync completion from AcceptEx\n");
			exit(-1);
		}

		// Completions are always asynchronous, so we are done here
	}

	void OnAccept(DWORD dwErrorCode, DWORD dwNumberofBytesTransferred)
	{
		if (dwErrorCode != 0)
		{
			printf("Accept failed, error code = %u\n", dwErrorCode);
			exit(-1);
		}

		if (s_trace)
		{
			printf("Connection accepted\n");
		}

		SOCKET s = _acceptSocket;
		_acceptSocket = NULL;

		// Kick off another async accept
		DoAccept();

		// Handle this connection
		Connection* c = new Connection(s, _isSsl);
		c->Run();
	}
};

void Start()
{
	WSADATA wsaData;
	int err;

	err = WSAStartup(MAKEWORD(2, 2), &wsaData);
	if (err != 0)
	{
		printf("WSAStartup failed\n");
		exit(-1);
	}

	// Create a listening socket
	ListenSocket* raw = new ListenSocket(5000, false);
	raw->Start();

	ListenSocket* ssl = new ListenSocket(5001, true);
	ssl->Start();
}

void PrintUsage()
{
	printf("Usage: nativeiocp [args]\n");
	printf("    -trace          Enable console trace output\n");
	printf("    -nosync         Don't process synchronous completions synchronously\n");
	printf("    -accept [n]     Issue [n] accept calls at startup\n");

	exit(-1);
}

void PrintUnkownArgumentError(char * argument)
{
	printf("Unexpected argument %s\n", argument);
	PrintUsage();
}

void PrintArgumentValueMissingError(char * argument)
{
	printf("Expected a value for argument %s\n", argument);
	PrintUsage();
}

void PrintArgumentValueInvalidError(char * argument, char * value)
{
	printf("Invalid value for argument %s: %s\n", argument, value);
	PrintUsage();
}

void ParseCommandLine(int argc, char **argv)
{
	int i = 1;
	while (i < argc)
	{
		if (_stricmp(argv[i], "-trace") == 0)
		{
			s_trace = true;
		}
		if (_stricmp(argv[i], "-nosync") == 0)
		{
			s_syncCompletions = false;
		}
		else if (_stricmp(argv[i], "-accept") == 0)
		{
			i++;
			if (i == argc)
			{
				PrintArgumentValueMissingError(argv[i - 1]);
			}

			s_acceptCount = atoi(argv[i]);
			if (s_acceptCount == 0)
			{
				PrintArgumentValueInvalidError(argv[i - 1], argv[i]);
			}
		}
		else
		{
			PrintUnkownArgumentError(argv[i]);
		}

		i++;
	}
}

int main(int argc, char ** argv)
{
	ParseCommandLine(argc, argv);

	Start();

	printf("Server running\n");
	Sleep(INFINITE);

    return 0;
}

