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

void TcpSession::Send(const std::vector<uint8_t>& buffer)
{
	if (buffer.size() > m_SendBufferSize) {
		std::cerr << "[Send 오류] 메시지 크기가 버퍼보다 큽니다.\n";
		return;
	}
	std::memcpy(m_SendBuffer, buffer.data(), buffer.size());
	AsyncWrite(m_SendBuffer, buffer.size());
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
		// 1. PacketHeader 파싱
		PacketHeader header;
		// RecvBuffer에서 PacketHeader 만큼 복사
		std::memcpy(&header, m_RecvBuffer, sizeof(PacketHeader));
		size_t offset = sizeof(PacketHeader);

		// 2. 타입에 따라 분기
		if (header.type == PacketType::CHAT)
		{
			std::vector<uint8_t> buffer(m_RecvBuffer, m_RecvBuffer + bytesTransferred);
			ChatPacket pkt = ChatPacket::Deserialize(buffer, offset);
			std::cout << "[" << pkt.sender << "] -> [" << pkt.receiver << "] : " << pkt.message << "\n";

			// 3. 브로드캐스트 또는 귓속말
			if (pkt.receiver == "ALL" || pkt.receiver.empty()) {
				// 전체 패킷 다시 구성
				auto body = pkt.Serialize();
				PacketHeader header{ PacketType::CHAT, static_cast<uint32_t>(body.size()) };

				std::vector<uint8_t> fullPacket;
				fullPacket.insert(fullPacket.end(), reinterpret_cast<uint8_t*>(&header), reinterpret_cast<uint8_t*>(&header) + sizeof(header));
				fullPacket.insert(fullPacket.end(), body.begin(), body.end());

				m_server->broad_cast(fullPacket);
			}
			else {
				auto body = pkt.Serialize();
				PacketHeader header{ PacketType::CHAT, static_cast<uint32_t>(body.size()) };

				std::vector<uint8_t> fullPacket;
				fullPacket.insert(fullPacket.end(), reinterpret_cast<uint8_t*>(&header), reinterpret_cast<uint8_t*>(&header) + sizeof(header));
				fullPacket.insert(fullPacket.end(), body.begin(), body.end());

				m_server->send_whisper(pkt.receiver, fullPacket);
			}
		}

		memset(m_RecvBuffer, 0, m_RecvBufferSize);
		AsyncRead();

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
