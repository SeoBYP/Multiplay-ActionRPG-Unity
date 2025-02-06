#undef UNICODE

#define WIN32_LEAN_AND_MEAN

#include <exception>
#include <iostream>
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
	try
	{
		ServerSocket server(8080);

		while (true)
		{
			SOCKET clientSocket = server.Accept();
			if (clientSocket == INVALID_SOCKET) {
				std::cerr << "Accept failed with error: " << WSAGetLastError() << std::endl;
				continue;
			}
			std::cout << "Client connected!" << std::endl;

			string message = server.Receive(clientSocket);
			std::cout << "Message from client: " << message << std::endl;
			server.Send(clientSocket , "Hello Client");

			// 1초 정도 대기 후 소켓 닫기 (클라이언트가 데이터 받을 시간 확보)
			Sleep(1000);

			closesocket(clientSocket);
		}

	}
	catch (const std::exception& e)
	{
		std::cerr << e.what() << std::endl;
	}
    return 0;
}
