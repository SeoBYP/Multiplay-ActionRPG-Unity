#pragma once
#include "Socket.h"

class ClientSocket : public Socket
{

public:
	ClientSocket(const string& serverIP, int port);
	bool Send(const string& message);
	string Receive(int bufferSize = 1024);
};
