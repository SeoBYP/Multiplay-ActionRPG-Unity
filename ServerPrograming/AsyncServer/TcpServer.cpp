#include "TcpServer.h"
#include "../AsyncServer/TcpSession.h"

TcpServer::TcpServer(asio::io_context& io_context, int port) :
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
void TcpServer::StartAccept()
{
	int nextSessionID = m_sessions.size() + 1;
	TcpSession* session = new TcpSession(m_io_context, this, nextSessionID);
	m_acceptor.async_accept(session->GetSocket(),
		[this, session](const boost::system::error_code& errorCode)
		{
			this->OnAccept(session, errorCode);
		});
}

void TcpServer::broad_cast(char* message, size_t message_size)
{
	for (auto session : m_sessions)
	{
		session->Send(message, message_size);
	}
}

void TcpServer::broad_cast(string& message)
{
	for (auto session : m_sessions)
	{
		session->Send(message);
	}
}


void TcpServer::send_whisper(const string& nickname, char* message, size_t message_size)
{
	for (auto session : m_sessions)
	{
		if (!session->GetNickname().compare(nickname))
			continue;
		session->Send(message, message_size);
	}
}
void TcpServer::send_whisper(const string& nickname, string& message)
{
	for (auto session : m_sessions)
	{
		if (session->GetNickname() != nickname)
			continue;
		session->Send(message);
	}
}

// 세션의 Start함수를 호출하여 통신을 시작하고
// StartAccept 다시 호출해서 클라이언트의 접속을 비동기적으로 대기
void TcpServer::OnAccept(TcpSession* session, const system::error_code& error_code)
{
	if (!error_code)
	{
		std::cout << "Accept" << std::endl;
		session->Start();
		m_sessions.push_back(session);
	}

	StartAccept();
}
