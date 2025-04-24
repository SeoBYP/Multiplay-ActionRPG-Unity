#pragma once
#include "PlayerSession.h"
#include <unordered_map>
#include <vector>
#include <memory>

using namespace std;

class PlayerSession;

class GameRoom : public std::enable_shared_from_this<GameRoom>
{
public:
	GameRoom(int roomId);

	bool AddSession(std::shared_ptr<PlayerSession> session);
	bool RemoveSession(int sessionId);

	void Broadcast(const vector<uint8_t>& packet);

	int GetRoomID() const { return m_roomId; }
	int GetPlayerCount();

private:
	int m_roomId;
	unordered_map<int, std::shared_ptr<PlayerSession>> m_sessions;
	mutex m_mutex;
};

