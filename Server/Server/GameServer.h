#pragma once

#include <iostream>
#include <boost/asio.hpp>
#include "Packet.h"
#include "GameRoom.h"
#include "PacketHandlerRegistry.h"

class PlayerSession;
using boost::asio::ip::tcp;
using namespace boost;
using namespace std;

class GameServer
{
public:
	GameServer(asio::io_context& io_context, int port);
	void StartAccept();
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

	PacketHandlerRegistry g_packetHandlers;
};