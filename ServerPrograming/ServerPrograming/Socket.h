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
	Socket();
	// 소멸시 소켓 닫기 및 WSA초기화
	virtual ~Socket();

	virtual void Shutdown() const;
};

