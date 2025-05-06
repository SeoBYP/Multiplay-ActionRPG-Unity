#include <Packet.h>
#include "PlayerSession.h"
#include "GameServer.h"
#include "ChatPacketHandler.h"
#include "SetNicknamePacketHandler.h"

PlayerSession::PlayerSession(boost::asio::io_context& io_context, int sessionID)
	: m_socket(io_context), m_sessionID(sessionID)
{
	memset(m_SendBuffer, 0, m_SendBufferSize);
	memset(m_RecvBuffer, 0, m_RecvBufferSize);

	InitPacketHandlers();
}

/// <summary>
/// 통신은 항상 서버와 클라이언트 간의 시작단계로 Read를 통해 이루어집니다.
/// 이 함수는 Read를 통해 통신을 시작합니다.
/// </summary>
void PlayerSession::Start()
{
	AsyncRead();
}

void PlayerSession::Send(char* message, size_t message_size)
{
	std::cout << "Send Message " << message << '\n';
	AsyncWrite(message, message_size);
}

void PlayerSession::Send(string message)
{
	std::cout << "Send Message " << message << '\n';
	AsyncWrite(message);
}

void PlayerSession::Send(const std::vector<uint8_t>& buffer)
{
	if (buffer.size() > m_SendBufferSize) {
		std::cerr << "[Send 오류] 메시지 크기가 버퍼보다 큽니다.\n";
		return;
	}
	std::memcpy(m_SendBuffer, buffer.data(), buffer.size()); // 이 줄 필수!

	std::cout << "[SEND FULL PACKET]: ";
	for (uint8_t b : buffer)
		printf("%02X ", b);
	printf("\n");

	AsyncWrite(m_SendBuffer, buffer.size());
}

tcp::socket& PlayerSession::GetSocket()
{
	return m_socket;
}



void PlayerSession::AsyncRead()
{
	m_socket.async_read_some(boost::asio::buffer(m_RecvBuffer, m_RecvBufferSize),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnRead(errorCode, bytesTransferred);
		});
}

void PlayerSession::OnRead(const boost::system::error_code& errorCode, const size_t bytesTransferred)
{
	std::cout << "OnRead: " << bytesTransferred << ", " << m_RecvBuffer << std::endl;
	if (!errorCode)
	{
		PacketHeader header;
		std::memcpy(&header, m_RecvBuffer, sizeof(PacketHeader));
		size_t offset = sizeof(PacketHeader);

		std::vector<uint8_t> buffer(m_RecvBuffer, m_RecvBuffer + bytesTransferred);
		g_PacketHandlerRegistry.Dispatch(this, header.type, buffer, offset);

		std::memset(m_RecvBuffer, 0, m_RecvBufferSize);
		AsyncRead();
	}
	else
	{
		std::cout << "Error: " << errorCode.message() << '\n';
	}
}

void PlayerSession::AsyncWrite(char* message, size_t size)
{
	memcpy(m_SendBuffer, message, size);

	async_write(m_socket, boost::asio::buffer(m_SendBuffer, size),
		[this](const boost::system::error_code& errorCode,
			const size_t bytesTransferred)
		{
			this->OnWrite(errorCode, bytesTransferred);
		});
}

void PlayerSession::AsyncWrite(string message)
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

void PlayerSession::OnWrite(const boost::system::error_code& errorCode, const size_t bytesTransferred)
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

void PlayerSession::InitPacketHandlers()
{
	g_PacketHandlerRegistry.Register(PacketType::CHAT, std::make_unique<ChatPacketHandler>());
	g_PacketHandlerRegistry.Register(PacketType::SET_NICKNAME_C2S, std::make_unique<SetNicknamePacketHandler>());
}
