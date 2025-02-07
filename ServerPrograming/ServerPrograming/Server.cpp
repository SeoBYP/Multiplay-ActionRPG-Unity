#undef UNICODE

#define WIN32_LEAN_AND_MEAN

#include <exception>
#include <iostream>
#include <map>
#include <ostream>
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdlib.h>
#include <stdio.h>
#include <system_error>

#include "ServerSocket.h"

#pragma comment(lib, "Ws2_32.lib")

#define DEFAULT_PORT "27015"
#define DEFAULT_BUFLEN 1024

using namespace std;

int main()
{
	vector<SOCKET> connectedSocket;
	try
	{
		ServerSocket server(8080);

		while (true)
		{
			SOCKET clientSocket = server.Accept();
			if (clientSocket == INVALID_SOCKET)
				continue;

			std::cout << "Client connected!" << std::endl;
			connectedSocket.push_back(clientSocket);

			while (true)
			{
				string message = server.Receive(clientSocket);
				if (message == "shutdown")
				{
					break;
				}
				std::cout << "Message from client: " << message << std::endl;
				// 클라이언트에게 응답
				server.Send(clientSocket, "Server received: " + message);
			}
			server.CloseSocket(clientSocket);
			connectedSocket.erase(remove(connectedSocket.begin(), connectedSocket.end(), clientSocket)
				,connectedSocket.end());

			std::cout << "Client disconnected." << std::endl;
			if (connectedSocket.empty())
				break;
		}
		server.Shutdown();
	}
	catch (const std::exception& e)
	{
		std::cerr << e.what() << std::endl;
	}
    return 0;
}
