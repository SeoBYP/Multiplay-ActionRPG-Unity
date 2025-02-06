#pragma once

#include <cstdio>
#include <iostream>
#include <stdexcept>
#include <vector>
#include <winsock2.h>
#include <ws2tcpip.h>

#pragma comment(lib, "Ws2_32.lib")

using namespace std;

// 소켓 클래스 설계
// WSA (Window Server API)

class Socket
{
protected:
	sockaddr_in address;
	SOCKET sock;

public:
	// Socket 생성자 클래스
	// 생성시 무조건 WSADATA를 초기화 해줘야 Socket을 사용할 수 있다.
	Socket() : sock(INVALID_SOCKET)
	{
		WSADATA wsaData;
		if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
			throw std::runtime_error("WSAStartup failed");
		}
	}

	// 소멸시 소켓 닫기 및 WSA초기화
	virtual ~Socket()
	{
		closesocket(sock);
		WSACleanup();
	}

	/// <summary>
	/// Socket 데이터를 송신하는 메서드
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	bool Send(const string& message)
	{
		int size = message.size();
		return send(sock, message.c_str(), size, 0);
	}

	/// <summary>
	/// Socket 데이터를 송신하는 메서드
	/// </summary>
	/// <param name="message"></param>
	/// <returns></returns>
	bool Send(SOCKET clientSocket, const string& message)
	{
		int size = message.size();
		return send(clientSocket, message.c_str(), size, 0);
	}


	/// <summary>
	/// Socket 데이터를 수신하는 메서드.
	/// </summary>
	/// <param name="bufferSize"></param>
	/// <returns></returns>
	string Receive(int bufferSize = 1024)
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

	string Receive(SOCKET clientSocket, int bufferSize = 1024)
	{
		vector<char> buffer(bufferSize);
		int byteReceived = recv(clientSocket, buffer.data(), bufferSize, 0);

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
};

