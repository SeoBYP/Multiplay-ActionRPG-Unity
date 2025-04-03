#include "TcpSession.h"
#include "TcpServer.h"

TcpSession::TcpSession(boost::asio::io_context& io_context, TcpServer* server, int sessionID)
	: m_socket(io_context), m_server(server), m_sessionID(sessionID)
{
	memset(m_SendBuffer, 0, m_SendBufferSize);
	memset(m_RecvBuffer, 0, m_RecvBufferSize);
}

/// <summary>
/// 통신은 항상 서버와 클라이언트 간의 시작단계로 Read를 통해 이루어집니다.
/// 이 함수는 Read를 통해 통신을 시작합니다.
/// </summary>
void TcpSession::Start()
{
	AsyncRead();
}

void TcpSession::Send(char* message, size_t message_size)
{
	std::cout << "Send Message " << message << '\n';
	AsyncWrite(message, message_size);
}

void TcpSession::Send(string message)
{
	std::cout << "Send Message " << message << '\n';

	AsyncWrite(message);
}

tcp::socket& TcpSession::GetSocket()
{
	return m_socket;
}

void TcpSession::SendPacket(const Packet& packet)
{
	//std::string serializedData = serializePacket(&packet);
	//AsyncWrite(serializedData);
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
		try
		{
			string receivedData(m_RecvBuffer, bytesTransferred);
			std::cout << "[TcpSession] 채팅 메시지 수신: " << receivedData << '\n';
			//auto packet = deserializePacket(receivedData);

			//if (!packet) {
			//	std::cerr << "[Error] 패킷 역직렬화 실패: packet == nullptr" << std::endl;
			//	return;
			//}

			//if (packet->getType() == PacketType::CHAT) {
			//	auto chatPacket = dynamic_cast<ChatPacket*>(packet.get());
			//	if (!chatPacket) {
			//		std::cerr << "[Error] ChatPacket 캐스팅 실패" << std::endl;
			//		return;
			//	}
			//	std::cout << "[TcpSession] 채팅 메시지 수신: " << chatPacket->getMessage() << '\n';
			//	m_server->broad_cast(*chatPacket);
			//}

			memset(m_RecvBuffer, 0, m_RecvBufferSize);
			AsyncRead();
		}
		catch (const std::exception&)
		{

		}
	}
	else
	{
		std::cout << "Error: " << errorCode.message() << '\n';
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
	std::cout << "OnWrite: " << bytesTransferred << '\n';
	if (!errorCode)
	{
		memset(m_SendBuffer, 0, m_SendBufferSize);
	}
	else
	{
		std::cout << "Error: " << errorCode.message() << '\n';
	}
}
