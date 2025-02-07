#include "ServerSocket.h"
#include "algorithm"

ServerSocket::ServerSocket(int port)
{
	sock = socket(AF_INET, SOCK_STREAM, 0);
	if (sock == INVALID_SOCKET)
		throw runtime_error("Socket create failed");

	address.sin_family = AF_INET;
	address.sin_port = htons(port);
	address.sin_addr.s_addr = INADDR_ANY;

	if (bind(sock, (SOCKADDR*)&address, sizeof(address)) == SOCKET_ERROR)
		throw runtime_error("Socket bind failed");

	if (listen(sock, 5) == SOCKET_ERROR)
		throw runtime_error("Socket listen failed");

	std::cout << "Server started on port " << port << std::endl;
}

SOCKET ServerSocket::Accept()
{
	SOCKET clientSocket = accept(sock, nullptr, nullptr);
	if (clientSocket == INVALID_SOCKET) {
		std::cerr << "Accept failed with error: " << WSAGetLastError() << std::endl;
	}
	return clientSocket;
}

bool ServerSocket::Send(SOCKET clientSocket, const string& message)
{
	int result = send(clientSocket, message.c_str(), static_cast<int>(message.size()), 0);
	return result != SOCKET_ERROR;
}

string ServerSocket::Receive(SOCKET clientSocket, int bufferSize)
{
	if (clientSocket == INVALID_SOCKET)
		throw runtime_error("Socket is InValied");

	string data;
	char buffer[1024] = {0};
	while (true)
	{
		int byteReceived = recv(clientSocket, buffer, sizeof(buffer), 0);
		if (byteReceived <= 0) {
			std::cerr << "Receive failed or connection closed." << std::endl;
			return "";
		}

		buffer[byteReceived] = '\0';
		data += buffer;

		if (data.find('\n') != string::npos)
		{
			break;
		}
	}

	return data.substr(0, data.find('\n'));
}

void ServerSocket::CloseSocket(SOCKET clientSocket) const
{
	closesocket(clientSocket);
}




