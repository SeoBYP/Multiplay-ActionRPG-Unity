#pragma once
#include "Socket.h"

class ServerSocket : public Socket
{

public:
	ServerSocket(int port);
	SOCKET Accept();
	bool Send(SOCKET clientSocket, const string& message);
	string Receive(SOCKET clientSocket, int bufferSize = 1024);
	void CloseSocket(SOCKET clientSocket) const;
};

