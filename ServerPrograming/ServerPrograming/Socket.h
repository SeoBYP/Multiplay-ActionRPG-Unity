#pragma once

#include <cstdio>
#include <iostream>
#include <stdexcept>
#include <vector>
#include <winsock2.h>
#include <ws2tcpip.h>

#pragma comment(lib, "Ws2_32.lib")

using namespace std;

// ���� Ŭ���� ����
// WSA (Window Server API)

class Socket
{
protected:
	sockaddr_in address;
	SOCKET sock;

public:
	// Socket ������ Ŭ����
	// ������ ������ WSADATA�� �ʱ�ȭ ����� Socket�� ����� �� �ִ�.
	Socket() : sock(INVALID_SOCKET)
	{
		WSADATA wsaData;
		if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
			throw std::runtime_error("WSAStartup failed");
		}
	}

	// �Ҹ�� ���� �ݱ� �� WSA�ʱ�ȭ
	virtual ~Socket()
	{
		closesocket(sock);
		WSACleanup();
	}

	/// <summary>
	/// Socket �����͸� �۽��ϴ� �޼���
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	bool Send(const string& message)
	{
		int size = message.size();
		return send(sock, message.c_str(), size, 0);
	}

	/// <summary>
	/// Socket �����͸� �۽��ϴ� �޼���
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	bool Send(SOCKET clientSocket, const string& message)
	{
		int size = message.size();
		return send(clientSocket, message.c_str(), size, 0);
	}


	/// <summary>
	/// Socket �����͸� �����ϴ� �޼���.
	/// </summary>
	/// <param name="bufferSize"></param>
	/// <returns></returns>
	string Receive(int bufferSize = 1024)
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

	string Receive(SOCKET clientSocket, int bufferSize = 1024)
	{
		vector<char> buffer(bufferSize);
		int byteReceived = recv(clientSocket, buffer.data(), bufferSize, 0);

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
};

