#include "TcpServer.h"
#include "../AsyncServer/TcpSession.h"

TcpServer::TcpServer(asio::io_context& io_context, int port) :
	m_acceptor(io_context, tcp::endpoint(tcp::v4(), port)),
	m_io_context(io_context)
{

}

/// <summary>
/// Ŭ���̾�Ʈ�� ����� ���� ������ ���� ��
/// �񵿱� accept �Լ��� accept_async�� ȣ��
/// ù��° ���ڷδ� ���� �� �Ҵ��� ������ ����
/// �ι��� ���ڷδ� �Լ��� ���������� ����� �� ȣ���� �Լ��� ����
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

// ������ Start�Լ��� ȣ���Ͽ� ����� �����ϰ�
// StartAccept �ٽ� ȣ���ؼ� Ŭ���̾�Ʈ�� ������ �񵿱������� ���
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
