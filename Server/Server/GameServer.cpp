#include "GameServer.h"
#include "PlayerSession.h"

GameServer::GameServer(asio::io_context& io_context, int port) :
	m_acceptor(io_context, tcp::endpoint(tcp::v4(), port)),
	m_io_context(io_context)
{
	
}

/// <summary>
/// 클라이언트와 통신을 위한 세션을 생성 후
/// 비동기 accept 함수인 accept_async를 호출
/// 첫번째 인자로는 연결 후 할당할 소켓을 전달
/// 두번쨰 인자로는 함수가 성공적으로 수행된 후 호출할 함수를 전달
/// </summary>
void GameServer::StartAccept()
{
	int nextSessionID = m_sessions.size() + 1;
	auto session = std::make_shared<PlayerSession>(m_io_context, nextSessionID);
	m_acceptor.async_accept(session->GetSocket(),
		[this, session](const boost::system::error_code& errorCode)
		{
			this->OnAccept(session, errorCode);
		});
}

// 세션의 Start함수를 호출하여 통신을 시작하고
// StartAccept 다시 호출해서 클라이언트의 접속을 비동기적으로 대기
void GameServer::OnAccept(std::shared_ptr<PlayerSession> session, const system::error_code& error_code)
{
	if (!error_code)
	{
		std::cout << "Accept" << std::endl;
		auto room = FindAvailableRoom();
		room->AddSession(session);
		session->Start();
		m_sessions.push_back(session);
	}

	StartAccept();
}

std::shared_ptr<GameRoom> GameServer::CreateRoom()
{
	int newId = ++m_nextRoomId;
	auto newRoom = std::make_shared<GameRoom>(newId);
	m_rooms[newId] = newRoom;
	return newRoom;
}

std::shared_ptr<GameRoom> GameServer::GetRoom(int roomId)
{
	for (auto& room : m_rooms)
	{
		if (room.first == roomId)
		{
			return room.second;
		}
	}
	return nullptr;
}

std::shared_ptr<GameRoom> GameServer::FindAvailableRoom()
{
	for (auto& room : m_rooms)
	{
		if (room.second->GetPlayerCount() < 4) // 예: 최대 4인
			return room.second;
	}

	int newId = ++m_nextRoomId;
	auto newRoom = std::make_shared<GameRoom>(newId);
	m_rooms[newId] = newRoom;
	return newRoom;
}
