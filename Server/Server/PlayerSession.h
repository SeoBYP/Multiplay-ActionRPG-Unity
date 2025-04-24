#pragma once

#include <iostream>
#include <boost/asio.hpp>
#include "ChatPacket.h"
#include "GameRoom.h"

class GameServer;
class GameRoom;

using boost::asio::ip::tcp;
using namespace boost;
using namespace std;

/// <summary>
/// 서버가 클라이언트와 연결 후 해당 클라이언트와 통신하기 위한 클래스
/// </summary>
class PlayerSession : public std::enable_shared_from_this<PlayerSession>
{
public:
	PlayerSession(boost::asio::io_context& io_context, GameServer* server, int sessionID);
	void Start();

	// Send
	void Send(char* message, size_t message_size);
	void Send(string message);
	void Send(const std::vector<uint8_t>& buffer);

	// GET
	tcp::socket& GetSocket();
	int GetSessionID() const { return  m_sessionID; }
	string GetNickname() const { return m_nickName; }
	std::shared_ptr<GameRoom> GetGameRoom() const { return m_gameRoom.lock(); }

	//Set
	void SetGameRoom(std::shared_ptr<GameRoom> room) { m_gameRoom = room; }
private:
	// Read
	void AsyncRead();
	void OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred);

	// Write
	void AsyncWrite(char* message, size_t size);
	void AsyncWrite(string message);
	void OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred);

private:
	int m_sessionID;
	string m_nickName;
	tcp::socket m_socket;
	GameServer* m_server;
	static const int m_RecvBufferSize = 1024;
	char m_RecvBuffer[m_RecvBufferSize];
	static const int m_SendBufferSize = 1024;
	char m_SendBuffer[m_RecvBufferSize];
	std::weak_ptr<GameRoom> m_gameRoom;
};
