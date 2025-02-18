#include "TcpSession.h"
#include "../AsyncServer/TcpServer.h"

TcpSession::TcpSession(boost::asio::io_context& io_context, TcpServer* server,int sessionID)
	: m_socket(io_context), m_server(server), m_sessionID(sessionID)
{
	memset(m_SendBuffer, 0, m_SendBufferSize);
	memset(m_RecvBuffer, 0, m_RecvBufferSize);
}

/// <summary>
/// ����� �׻� ������ Ŭ���̾�Ʈ ���� ���۴ܰ�� Read�� ���� �̷�����ϴ�.
/// �� �Լ��� Read�� ���� ����� �����մϴ�.
/// </summary>
void TcpSession::Start()
{
	AsyncRead();
}

void TcpSession::Send(char* message, size_t message_size)
{
	std::cout << "Send Message " << message << std::endl;
	AsyncWrite(message, message_size);
}

void TcpSession::Send(string message)
{
	std::cout << "Send Message " << message << std::endl;
	
	AsyncWrite(message);
}

tcp::socket& TcpSession::GetSocket()
{
	return m_socket;
}

void TcpSession::AsyncRead()
{
	m_socket.async_read_some(boost::asio::buffer(m_RecvBuffer, m_RecvBufferSize),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnRead(errorCode, bytesTransferred);
		});
}

void TcpSession::OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	std::cout << "OnRead: " << bytesTransferred << ", " << m_RecvBuffer << std::endl;
	if (!errorCode)
	{
		string receivedMessage(m_RecvBuffer, bytesTransferred);
		// �г����� �������� �ʾ������� ó�� �Է��� �г������� �Ѵ�.
		if (m_nickName.empty())
		{
			m_nickName = receivedMessage;
		}
		else if (receivedMessage.rfind("/w ",0) != string::npos)
		{
			size_t spacePos = receivedMessage.find(" ", 3);
			if (spacePos != string::npos)
			{
				string targetNickName = receivedMessage.substr(3, spacePos - 3);
				string message = receivedMessage.substr(spacePos + 1);
				message.insert(0, m_nickName + " ");
				// �����׵� ������
				AsyncWrite(message);
				// ��뿡�Ե� ������.
				m_server->send_whisper(targetNickName, message);
			}
		}
		else
		{
			receivedMessage.insert(0, m_nickName + " ");
			m_server->broad_cast(receivedMessage);
		}
		memset(m_RecvBuffer, 0, m_RecvBufferSize);
		AsyncRead();
	}
	else
	{
		std::cout << "Error: " << errorCode.message() << std::endl;
	}
}

void TcpSession::AsyncWrite(char* message, size_t size)
{
	memcpy(m_SendBuffer, message, size);

	async_write(m_socket, boost::asio::buffer(m_SendBuffer, size),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnWrite(errorCode, bytesTransferred);
		});
}

void TcpSession::AsyncWrite(string message)
{
	size_t size = message.size();
	memcpy(m_SendBuffer, message.c_str(), size);

	async_write(m_socket, boost::asio::buffer(m_SendBuffer, size),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnWrite(errorCode, bytesTransferred);
		});
}

void TcpSession::OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	std::cout << "OnWrite: " << bytesTransferred << std::endl;
	if (!errorCode)
	{
		memset(m_SendBuffer, 0, m_SendBufferSize);
	}
	else
	{
		std::cout << "Error: " << errorCode.message() << std::endl;
	}
}
