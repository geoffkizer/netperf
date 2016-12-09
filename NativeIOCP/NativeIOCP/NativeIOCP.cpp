// NativeIOCP.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"

// Forward decls

class Connection;
void QueueConnectionHandler();

SOCKET s_listenSocket = INVALID_SOCKET;

#define RESPONSE "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n"
#define PIPELINED_RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE RESPONSE

const char s_responseMessage[] = PIPELINED_RESPONSE;
const int s_responseMessageLength = sizeof(s_responseMessage) - 1; // exclude trailing null

const int s_expectedReadSize = 848;

bool s_trace = false;


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

		err = SetFileCompletionNotificationModes((HANDLE)_socket, FILE_SKIP_COMPLETION_PORT_ON_SUCCESS);
		if (err == 0)
		{
			printf("SetFileCompletionNotificationModes of accepted socket failed with error: %u\n", GetLastError());
			exit(-1);
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

		// CONSIDER: May need to loop on write, probably isn't necessary for benchmarking

		DoRead();
	}

	void OnCompletion(DWORD dwErrorCode, DWORD dwNumberofBytesTransferred)
	{
		if (dwErrorCode != 0)
		{
			printf("Socket I/O failed, error code = %u\n", dwErrorCode);
			exit(-1);
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

#if 0
void CALLBACK ListenSocketCallback(
	DWORD dwErrorCode,
	DWORD dwNumberOfBytesTransfered,
	LPOVERLAPPED lpOverlapped)
{
	Connection::OnAccept(dwErrorCode, dwNumberOfBytesTransfered, lpOverlapped);
}
#endif

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

	// Start running by queuing a single connection handler to run
	QueueConnectionHandler();
}

int main()
{
	Start();

	printf("Server running\n");
	Sleep(INFINITE);

    return 0;
}

