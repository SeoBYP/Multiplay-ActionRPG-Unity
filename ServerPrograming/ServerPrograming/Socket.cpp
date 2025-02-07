#include "Socket.h"

Socket::Socket() : address(), sock(INVALID_SOCKET)
{
	WSADATA wsaData;
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0)
	{
		throw std::runtime_error("WSAStartup failed");
	}
}

Socket::~Socket()
{
	closesocket(sock);
	WSACleanup();
}

void Socket::Shutdown() const
{
	closesocket(sock);
	WSACleanup();
}
