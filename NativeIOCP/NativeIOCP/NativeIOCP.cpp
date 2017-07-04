// NativeIOCP.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"

// Forward decls

class Connection;
void QueueConnectionHandler();

SOCKET s_listenSocket = INVALID_SOCKET;

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

class Connection : OVERLAPPED
{
private:
	SOCKET _socket;
	BYTE _readBuffer[4096];
	
	enum State
	{
		IsReading = 0,
		IsWriting = 1
	};

	State _state;

	OVERLAPPED* ToOverlapped()
	{
		return static_cast<OVERLAPPED *>(this);
	}

	static Connection* FromOverlapped(OVERLAPPED * o)
	{
		return static_cast<Connection *>(o);
	}

	void DoAccept()
	{
		_socket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		if (_socket == INVALID_SOCKET)
		{
			printf("Create accept socket failed with error: %u\n", WSAGetLastError());
			exit(-1);
		}

		memset(ToOverlapped(), 0, sizeof(OVERLAPPED));
		BOOL success = AcceptEx(s_listenSocket, _socket, &_readBuffer, 0,
			sizeof(sockaddr_in6) + 16, sizeof(sockaddr_in6) + 16, NULL, ToOverlapped());
		if (success == FALSE)
		{
			int error = WSAGetLastError();
			if (error != ERROR_IO_PENDING)
			{
				printf("AcceptEx failed with error: %u\n", WSAGetLastError());
				exit(-1);
			}
		}
	}

	void OnAccept(DWORD dwErrorCode)
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

		// Spawn another work item to handle next connection
		QueueConnectionHandler();

		int err = BindIoCompletionCallback((HANDLE)_socket, &Connection::CompletionCallback, 0);
		if (err == 0)
		{
			printf("BindIoCompletionCallback of accepted socket failed with error: %u\n", GetLastError());
			exit(-1);
		}

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

	void DoRead()
	{
		_state = IsReading;

		WSABUF wbuf;
		wbuf.buf = (CHAR*)&_readBuffer;
		wbuf.len = 4096;
		DWORD bytesReceived;
		DWORD flags = 0;
		int err = WSARecv(_socket, &wbuf, 1, &bytesReceived, &flags, (WSAOVERLAPPED *)ToOverlapped(), NULL);
		if (err == 0)
		{
			// Synchronous completion
			if (s_syncCompletions)
				OnReadComplete(bytesReceived);
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

	void OnReadComplete(int bytesRead)
	{
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

		_state = IsWriting;

		WSABUF wbuf;
		wbuf.buf = (CHAR*)s_responseMessage;
		wbuf.len = s_responseMessageLength;
		DWORD bytes;
		int err = WSASend(_socket, &wbuf, 1, &bytes, 0, (WSAOVERLAPPED*)ToOverlapped(), NULL);
		if (err == 0)
		{
			// Synchronous completion
			if (s_syncCompletions)
				OnWriteComplete(bytes);
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

	void OnWriteComplete(int bytesWritten)
	{
		if (s_trace)
		{
			printf("Write complete, bytesRead = %u\n", bytesWritten);
		}

		if (bytesWritten != s_responseMessageLength)
		{
			printf("Unexpected write size, bytesWritten = %u", bytesWritten);
			exit(-1);
		}

		// CONSIDER: May need to loop on write, probably isn't necessary for benchmarking

		DoRead();
	}

	void OnCompletion(DWORD dwErrorCode, DWORD dwNumberofBytesTransferred)
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

		switch (_state)
		{
		case IsReading:
			OnReadComplete(dwNumberofBytesTransferred);
			break;

		case IsWriting:
			OnWriteComplete(dwNumberofBytesTransferred);
			break;

		default:
			printf("Unexpected connection state\n");
			exit(-1);
		}
	}

	static void CALLBACK CompletionCallback(
		DWORD dwErrorCode,
		DWORD dwNumberOfBytesTransfered,
		LPOVERLAPPED lpOverlapped)
	{
		FromOverlapped(lpOverlapped)->OnCompletion(dwErrorCode, dwNumberOfBytesTransfered);
	}

public:
	Connection()
	{
	}

	static DWORD WINAPI Run(LPVOID param)
	{
		Connection* c = new Connection();
		c->DoAccept();
		return 0;
	}

	static void CALLBACK ListenSocketCallback(
		DWORD dwErrorCode,
		DWORD dwNumberOfBytesTransfered,
		LPOVERLAPPED lpOverlapped)
	{
		FromOverlapped(lpOverlapped)->OnAccept(dwErrorCode);
	}
};

void QueueConnectionHandler()
{
	if (QueueUserWorkItem(&Connection::Run, NULL, 0) == FALSE)
	{
		printf("QueueUserWorkItem failed with error: %u\n", GetLastError());
		exit(-1);
	}
}

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
	s_listenSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
	if (s_listenSocket == INVALID_SOCKET) 
	{
		printf("Create of ListenSocket socket failed with error: %u\n", WSAGetLastError());
		exit(-1);
	}

	// Bind to the thread pool IOCP
	err = BindIoCompletionCallback((HANDLE)s_listenSocket, &Connection::ListenSocketCallback, 0);
	if (err == 0)
	{
		printf("BindIoCompletionCallback of ListenSocket socket failed with error: %u\n", GetLastError());
		exit(-1);
	}

	// Bind to IP/Port
	sockaddr_in sa;
	sa.sin_family = AF_INET;
	sa.sin_addr.S_un.S_addr = INADDR_ANY;
	sa.sin_port = htons(5000);
	if (bind(s_listenSocket, (SOCKADDR *)&sa, sizeof(sa)) == SOCKET_ERROR) 
	{
		printf("bind failed with error: %u\n", WSAGetLastError());
		exit(-1);
	}

	// Listen
	err = listen(s_listenSocket, SOMAXCONN);
	if (err == SOCKET_ERROR) 
	{
		printf("listen failed with error: %u\n", WSAGetLastError());
		exit(-1);
	}

	// Spawn async accepts
	for (int i = 0; i < s_acceptCount; i++)
		QueueConnectionHandler();
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

