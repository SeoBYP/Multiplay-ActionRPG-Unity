#define WIN32_LEAN_AND_MEAN

#include <exception>
#include <iostream>
#include <ostream>
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdlib.h>
#include <stdio.h>
#include <string>

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
		while (true)
		{
			string input;
			std::cout << "메시지를 입력하세요 : ";
			std::getline(std::cin, input);

			if (input.size() <= 0)
			{
				continue;
			}

			if (input == "shutdown")
			{
				client.Send(input);
				client.Shutdown();
				break;
			}

			client.Send(input);
			string response = client.Receive();
			std::cout << "Received: " << response << std::endl;
		}
	}
	catch (const std::exception& e)
	{
		std::cerr << e.what() << std::endl;
	}
    return 0;
}
