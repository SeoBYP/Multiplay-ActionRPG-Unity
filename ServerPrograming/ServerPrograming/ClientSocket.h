#pragma once
#include "Socket.h"

class ClientSocket : public Socket
{
public:
	ClientSocket(const string& serverIP, int port)
	{
		sock = socket(AF_INET, SOCK_STREAM, 0);
		if (sock == INVALID_SOCKET)
			throw runtime_error("Socket create failed");

		address.sin_family = AF_INET;
		address.sin_port = htons(port);

		if (inet_pton(AF_INET, serverIP.c_str(), &address.sin_addr) <= 0)
			throw std::runtime_error("Invalid address");

		if (connect(sock, (SOCKADDR*)&address, sizeof(address)) == SOCKET_ERROR)
			throw std::runtime_error("Connection failed");

		std::cout << "Connected to server " << serverIP << ":" << port << std::endl;
	}
};
