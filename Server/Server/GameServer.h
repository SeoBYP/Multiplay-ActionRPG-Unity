#pragma once

#include <iostream>
#include <boost/asio.hpp>
#include "Packet.h"
#include "GameRoom.h"

class PlayerSession;
using boost::asio::ip::tcp;
using namespace boost;
using namespace std;

class GameServer
{
public:
	GameServer(asio::io_context& io_context, int port);
	void StartAccept();
	//void broad_cast(char* message, size_t message_size);
	//void broad_cast(string& message);
	//void broad_cast(const std::vector<uint8_t>& message);
	//void broad_cast(const Packet& packet);

	//void send_whisper(const string& nickname, char* message, size_t message_size);
	//void send_whisper(const string& nickname, string& message);
	//void send_whisper(const string& nickname, const std::vector<uint8_t>& message);
	//void send_whisper(const string& nickname, const Packet& packet);
	void OnAccept(std::shared_ptr<PlayerSession>, const system::error_code& error_code);

	std::shared_ptr<GameRoom> CreateRoom();
	std::shared_ptr<GameRoom> GetRoom(int roomId);
	std::shared_ptr<GameRoom> FindAvailableRoom();

private:
	int m_nextRoomId;
	tcp::acceptor m_acceptor;
	asio::io_context& m_io_context;
	vector<std::shared_ptr<PlayerSession>> m_sessions;
	std::unordered_map<int, std::shared_ptr<GameRoom>> m_rooms;
};