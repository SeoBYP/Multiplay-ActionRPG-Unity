#pragma once
#include "Socket.h"

class ServerSocket : public Socket
{
public:
	ServerSocket(int port)
	{
		sock = socket(AF_INET, SOCK_STREAM, 0);
		if (sock == INVALID_SOCKET)
			throw runtime_error("Socket create failed");

		address.sin_family = AF_INET;
		address.sin_port = htons(port);
		address.sin_addr.s_addr = INADDR_ANY;

		if (bind(sock,(SOCKADDR*)&address,sizeof(address)) == SOCKET_ERROR)
			throw runtime_error("Socket bind failed");

		if (listen(sock, 5) == SOCKET_ERROR)
			throw runtime_error("Socket listen failed");

		std::cout << "Server started on port " << port << std::endl;
	}

	SOCKET Accept()
	{
		SOCKET clientSocket = accept(sock, nullptr, nullptr);
		if (clientSocket == INVALID_SOCKET)
			throw runtime_error("Socket accept failed");
		return clientSocket;
	}
};

