#include "ClientSocket.h"

ClientSocket::ClientSocket(const string& serverIP, int port)
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

bool ClientSocket::Send(const string& message)
{
	// 어떻게 동작하
	string modified = message + "\n";
	return send(sock, modified.c_str(), static_cast<int>(modified.size()), 0);
}

string ClientSocket::Receive(int bufferSize)
{
	if (sock == INVALID_SOCKET) {
		std::cerr << "잘못된 소켓입니다. recv()를 호출할 수 없습니다." << std::endl;
		return "";
	}

	vector<char> buffer(bufferSize);
	int byteReceived = recv(sock, buffer.data(), bufferSize, 0);

	if (byteReceived == 0) {
		std::cout << "클라이언트가 연결을 종료했습니다." << std::endl;
		return "";
	}
	else if (byteReceived == SOCKET_ERROR) {
		int errorCode = WSAGetLastError();
		if (errorCode == WSAECONNRESET) {
			std::cout << "클라이언트의 연결이 강제로 종료되었습니다." << std::endl;
		}
		else {
			std::cerr << "recv() 실패, 오류 코드: " << errorCode << std::endl;
		}
		return "";
	}
	return std::string(buffer.begin(), buffer.begin() + byteReceived);
}
