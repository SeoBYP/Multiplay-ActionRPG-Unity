#include "GameRoom.h"

GameRoom::GameRoom(int roomId)
	: m_roomId(roomId)
{
}

bool GameRoom::AddSession(std::shared_ptr<PlayerSession> session)
{
	std::lock_guard<mutex> lock(m_mutex);
	m_sessions[session->GetSessionID()] = session;
	session->SetGameRoom(shared_from_this());
	return false;
}

bool GameRoom::RemoveSession(int sessionId)
{
	std::lock_guard<mutex> lock(m_mutex);
	m_sessions.erase(sessionId);
	return false;
}

void GameRoom::Broadcast(const std::vector<uint8_t>& packet)
{
	std::lock_guard<mutex> lock(m_mutex);
	for (auto& session : m_sessions)
	{
		if (session.second)
		{
			session.second->Send(packet);
		}
	}
}

int GameRoom::GetPlayerCount()
{
	std::lock_guard<mutex> lock(m_mutex);
	return static_cast<int>(m_sessions.size());
}
