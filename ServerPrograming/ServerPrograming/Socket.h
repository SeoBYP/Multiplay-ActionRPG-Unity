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
	Socket();
	// �Ҹ�� ���� �ݱ� �� WSA�ʱ�ȭ
	virtual ~Socket();

	virtual void Shutdown() const;
};

