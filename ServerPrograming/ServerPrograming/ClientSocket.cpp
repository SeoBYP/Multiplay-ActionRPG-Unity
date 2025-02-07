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
	// ��� ������
	string modified = message + "\n";
	return send(sock, modified.c_str(), static_cast<int>(modified.size()), 0);
}

string ClientSocket::Receive(int bufferSize)
{
	if (sock == INVALID_SOCKET) {
		std::cerr << "�߸��� �����Դϴ�. recv()�� ȣ���� �� �����ϴ�." << std::endl;
		return "";
	}

	vector<char> buffer(bufferSize);
	int byteReceived = recv(sock, buffer.data(), bufferSize, 0);

	if (byteReceived == 0) {
		std::cout << "Ŭ���̾�Ʈ�� ������ �����߽��ϴ�." << std::endl;
		return "";
	}
	else if (byteReceived == SOCKET_ERROR) {
		int errorCode = WSAGetLastError();
		if (errorCode == WSAECONNRESET) {
			std::cout << "Ŭ���̾�Ʈ�� ������ ������ ����Ǿ����ϴ�." << std::endl;
		}
		else {
			std::cerr << "recv() ����, ���� �ڵ�: " << errorCode << std::endl;
		}
		return "";
	}
	return std::string(buffer.begin(), buffer.begin() + byteReceived);
}
