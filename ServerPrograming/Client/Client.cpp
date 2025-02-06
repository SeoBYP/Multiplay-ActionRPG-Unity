#define WIN32_LEAN_AND_MEAN

#include <exception>
#include <iostream>
#include <ostream>
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdlib.h>
#include <stdio.h>

#include "../ServerPrograming/ClientSocket.h"

#pragma comment (lib, "Ws2_32.lib")
#pragma comment (lib, "Mswsock.lib")
#pragma comment (lib, "AdvApi32.lib")

#define DEFAULT_BUFLEN 512
#define DEFAULT_PORT "27015"

using namespace std;

int main()
{
	try
	{
		ClientSocket client("127.0.0.1", 8080);
		client.Send("Hello Socket");

		string response = client.Receive();
		if (!response.empty())
		{
			std::cout << "Received: " << response << std::endl;
		}
	}
	catch (const std::exception& e)
	{
		std::cerr << e.what() << std::endl;
	}


    return 0;
}
